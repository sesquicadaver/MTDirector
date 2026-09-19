using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-RPC-FAULT-01: operator ErrorText includes Controller ErrorDetail trailers.
/// Unary deadlines and Watch streams must not regress.
/// </summary>
public sealed class DeskRpcFault01DesktopErrorDetailLivingSpecTests
{
    [Fact]
    public void Ac1TrailerMapsCodeCorrelationAndFallback()
    {
        Guid correlation = Guid.Parse("11111111-1111-1111-1111-111111111111");
        ErrorDetail detail = new()
        {
            Code = "validation",
            Retryable = false,
            CorrelationId = DesktopProtoUuid.FromGuid(correlation),
            SanitizedDetail = "name required",
        };
        RpcException withTrailer = new(
            new Status(StatusCode.InvalidArgument, "name required"),
            Trailer(detail));
        Assert.Equal(
            "validation (correlation 11111111-1111-1111-1111-111111111111): name required",
            DesktopRpcFaultText.Format(withTrailer));

        detail.Retryable = true;
        detail.Code = "dependency";
        detail.SanitizedDetail = "controller unavailable";
        RpcException retryable = new(
            new Status(StatusCode.Unavailable, ""),
            Trailer(detail));
        Assert.Equal(
            "dependency (correlation 11111111-1111-1111-1111-111111111111, retryable): controller unavailable",
            DesktopRpcFaultText.Format(retryable));

        RpcException detailOnly = new(new Status(StatusCode.Unavailable, "upstream reset"), new Metadata());
        Assert.Equal("upstream reset", DesktopRpcFaultText.Format(detailOnly));

        RpcException empty = new(new Status(StatusCode.DeadlineExceeded, "  "), new Metadata());
        Assert.Equal("DeadlineExceeded", DesktopRpcFaultText.Format(empty));

        RpcException malformed = new(
            new Status(StatusCode.Internal, "boom"),
            new Metadata { { DesktopRpcFaultText.ErrorDetailMetadataKey, new byte[] { 0x80 } } });
        Assert.Equal("boom", DesktopRpcFaultText.Format(malformed));

        Assert.Equal(
            DesktopRpcFaultText.ErrorDetailMetadataKey,
            GrpcApplicationErrorMapperKey());
    }

    [Fact]
    public void Ac2CallSitesDocsAndQueueLockFaultMappingWithoutDeadlineRegression()
    {
        string root = RepoRoot();
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string unary = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Configuration/DesktopOptions.cs"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan52 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-52-desktop-grpc-error-detail.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string handler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));

        Assert.Contains("mfc-error-detail-bin", helper, StringComparison.Ordinal);
        Assert.Contains("correlation", helper, StringComparison.Ordinal);
        Assert.Contains("Retryable", helper, StringComparison.Ordinal);
        Assert.Contains("public int UnaryCallTimeoutSeconds { get; init; } = 30;", options, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", unary, StringComparison.Ordinal);
        Assert.Contains("must be greater than 0", unary, StringComparison.Ordinal);

        int sites = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "src/Mfc.Desktop/ViewModels"), "*ViewModel.cs"))
        {
            string viewModel = File.ReadAllText(path);
            sites += Count(viewModel, "DesktopRpcFaultText.Format");
            Assert.DoesNotContain("ErrorText = ex.Status.Detail", viewModel, StringComparison.Ordinal);
        }

        Assert.Equal(14, sites);

        string servicesDir = Path.Combine(root, "src/Mfc.Desktop/Services");
        foreach (string clientPath in Directory.EnumerateFiles(servicesDir, "Grpc*Client.cs"))
        {
            string client = File.ReadAllText(clientPath);
            Assert.Contains("DesktopGrpcUnaryCall.For", client, StringComparison.Ordinal);
            AssertWatchStreamsHaveNoUnaryDeadline(client);
        }

        Assert.Contains("CancelAfter(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds))", connection, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", connection, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", handler, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);

        DesktopOptions desktopOptions = new() { UnaryCallTimeoutSeconds = 30 };
        CallOptions call = DesktopGrpcUnaryCall.For(desktopOptions, new Metadata(), CancellationToken.None);
        Assert.NotNull(call.Deadline);

        Assert.Contains("DESK-RPC-FAULT-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", installation, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", local, StringComparison.Ordinal);
        Assert.Contains("DeskRpcFault01DesktopErrorDetailLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-354 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-354 | [#1114](https://github.com/sesquicadaver/MTDirector/issues/1114) | DESK-RPC-FAULT-01 — Map Controller ErrorDetail trailer into operator ErrorText | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-385 (#1176)", roadmap, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-354)", plan52, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText.Format", plan52, StringComparison.Ordinal);
    }

    private static Metadata Trailer(ErrorDetail detail)
        => new()
        {
            { DesktopRpcFaultText.ErrorDetailMetadataKey, detail.ToByteArray() },
        };

    private static string GrpcApplicationErrorMapperKey()
    {
        string root = RepoRoot();
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        const string marker = "ErrorDetailMetadataKey = \"";
        int start = mapper.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        int end = mapper.IndexOf('"', start);
        return mapper[start..end];
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
