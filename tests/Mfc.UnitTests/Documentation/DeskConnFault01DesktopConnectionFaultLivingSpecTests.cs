using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-CONN-FAULT-01: connection AuthenticationFailed LastError uses DesktopRpcFaultText
/// so shell status includes the correlation id and is never empty.
/// </summary>
public sealed class DeskConnFault01DesktopConnectionFaultLivingSpecTests
{
    [Fact]
    public void Ac1AuthStatusIncludesCorrelationAndNonEmptyFallback()
    {
        Guid correlation = Guid.Parse("22222222-2222-2222-2222-222222222222");
        ErrorDetail detail = new()
        {
            Code = "auth",
            Retryable = false,
            CorrelationId = DesktopProtoUuid.FromGuid(correlation),
            SanitizedDetail = "token rejected",
        };
        RpcException withTrailer = new(
            new Status(StatusCode.Unauthenticated, ""),
            Trailer(detail));
        string formatted = DesktopRpcFaultText.Format(withTrailer);
        Assert.Contains("correlation 22222222-2222-2222-2222-222222222222", formatted, StringComparison.Ordinal);
        Assert.Contains("auth", formatted, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(formatted));

        RpcException empty = new(new Status(StatusCode.PermissionDenied, "  "), new Metadata());
        Assert.Equal("PermissionDenied", DesktopRpcFaultText.Format(empty));
    }

    [Fact]
    public void Ac2ConnectionSitesDocsAndPriorLocks()
    {
        string root = RepoRoot();
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string shell = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/ShellViewModel.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string unary = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan54 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-54-desktop-connection-status-fault-text.md"));

        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
        Assert.DoesNotContain("ex.Status.Detail", connection, StringComparison.Ordinal);
        Assert.Equal(2, Count(connection, "Health check timed out."));
        Assert.Contains("SetState(ControllerConnectionState.TlsError, ex.Message)", connection, StringComparison.Ordinal);
        Assert.Contains("CancelAfter(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds))", connection, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", connection, StringComparison.Ordinal);
        Assert.Contains("ErrorText = _connection.LastError", shell, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", helper, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", unary, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}", mapper, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);

        int sites = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "src/Mfc.Desktop/ViewModels"), "*ViewModel.cs"))
        {
            sites += Count(File.ReadAllText(path), "DesktopRpcFaultText.Format");
        }

        Assert.Equal(14, sites);

        Assert.Contains("DESK-CONN-FAULT-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", installation, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", local, StringComparison.Ordinal);
        Assert.Contains("DeskConnFault01DesktopConnectionFaultLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-362 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-362 | [#1130](https://github.com/sesquicadaver/MTDirector/issues/1130) | DESK-CONN-FAULT-01 — Route connection AuthenticationFailed status through DesktopRpcFaultText | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-368 (#1143)", roadmap, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-362)", plan54, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText.Format", plan54, StringComparison.Ordinal);

        DesktopOptions options = new() { UnaryCallTimeoutSeconds = 30 };
        CallOptions call = DesktopGrpcUnaryCall.For(options, new Metadata(), CancellationToken.None);
        Assert.NotNull(call.Deadline);
    }

    private static Metadata Trailer(ErrorDetail detail)
        => new()
        {
            { DesktopRpcFaultText.ErrorDetailMetadataKey, detail.ToByteArray() },
        };

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
