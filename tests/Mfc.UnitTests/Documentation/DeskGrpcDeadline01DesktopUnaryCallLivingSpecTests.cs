using Grpc.Core;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-GRPC-DEADLINE-01: Desktop unary gRPC calls use a fail-closed deadline.
/// Watch / server-streaming RPCs stay unbounded. Do not regress transport limits.
/// </summary>
public sealed class DeskGrpcDeadline01DesktopUnaryCallLivingSpecTests
{
    [Fact]
    public void Ac1UnaryCallOptionsUseFiniteFailClosedDeadline()
    {
        DesktopOptions options = new() { UnaryCallTimeoutSeconds = 30 };
        DateTime before = DateTime.UtcNow;
        CallOptions call = DesktopGrpcUnaryCall.For(options, new Metadata(), CancellationToken.None);
        Assert.NotNull(call.Deadline);
        Assert.InRange(call.Deadline!.Value, before.AddSeconds(29), DateTime.UtcNow.AddSeconds(31));

        Assert.Throws<InvalidOperationException>(() =>
            DesktopGrpcUnaryCall.For(new DesktopOptions { UnaryCallTimeoutSeconds = 0 }, new Metadata(), CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() =>
            DesktopGrpcUnaryCall.For(new DesktopOptions { UnaryCallTimeoutSeconds = -1 }, new Metadata(), CancellationToken.None));
    }

    [Fact]
    public void Ac2CallSitesDocsAndQueueLockUnaryDeadlinePolicy()
    {
        string root = RepoRoot();
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Configuration/DesktopOptions.cs"));
        string appsettings = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/appsettings.json"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string handler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan51 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-51-desktop-grpc-unary-deadline.md"));

        Assert.Contains("public int UnaryCallTimeoutSeconds { get; init; } = 30;", options, StringComparison.Ordinal);
        Assert.Contains("\"UnaryCallTimeoutSeconds\": 30", appsettings, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", helper, StringComparison.Ordinal);
        Assert.Contains("seconds <= 0", helper, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", helper, StringComparison.Ordinal);

        string servicesDir = Path.Combine(root, "src/Mfc.Desktop/Services");
        int unarySites = 0;
        foreach (string clientPath in Directory.EnumerateFiles(servicesDir, "Grpc*Client.cs"))
        {
            string client = File.ReadAllText(clientPath);
            unarySites += Count(client, "DesktopGrpcUnaryCall.For");
            AssertWatchStreamsHaveNoUnaryDeadline(client);
        }

        Assert.Equal(64, unarySites);

        Assert.Contains("CancelAfter(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds))", connection, StringComparison.Ordinal);
        Assert.DoesNotContain("UnaryCallTimeoutSeconds", connection, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", connection, StringComparison.Ordinal);
        Assert.Contains("MaxSendMessageSize = GrpcTransportLimits.MaxMessageBytes", connection, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", handler, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingTimeout = GrpcHttp2KeepAlive.PingTimeout", handler, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate = null", program, StringComparison.Ordinal);

        Assert.Contains("UnaryCallTimeoutSeconds", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", installation, StringComparison.Ordinal);
        Assert.Contains("UnaryCallTimeoutSeconds", local, StringComparison.Ordinal);
        Assert.Contains("DeskGrpcDeadline01DesktopUnaryCallLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-350 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-350 | [#1106](https://github.com/sesquicadaver/MTDirector/issues/1106) | DESK-GRPC-DEADLINE-01 — Desktop unary gRPC CallOptions deadline policy | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-353 (#1112)", roadmap, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-350)", plan51, StringComparison.Ordinal);
        Assert.Contains("UnaryCallTimeoutSeconds", plan51, StringComparison.Ordinal);
    }

    private static void AssertWatchStreamsHaveNoUnaryDeadline(string client)
    {
        foreach (string marker in new[] { "client.Watch(", "client.WatchCapture(" })
        {
            int index = 0;
            while (true)
            {
                int start = client.IndexOf(marker, index, StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                int end = client.IndexOf(';', start);
                Assert.True(end > start);
                string call = client[start..end];
                Assert.Contains("ActorHeaders()", call, StringComparison.Ordinal);
                Assert.DoesNotContain("DesktopGrpcUnaryCall", call, StringComparison.Ordinal);
                index = end + 1;
            }
        }
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + value.Length;
        }
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
