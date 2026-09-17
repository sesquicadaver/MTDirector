using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-GRPC-KEEPALIVE-01: Controller Kestrel Http2 + Desktop SocketsHttpHandler
/// KeepAlivePingDelay/Timeout via shared GrpcHttp2KeepAlive (60s / 30s).
/// Do not regress MSGSIZE/BODY/health/metrics/tracing.
/// </summary>
public sealed class CtrlGrpcKeepalive01ControllerDesktopHttp2KeepaliveLivingSpecTests
{
    [Fact]
    public void Ac1SharedConstantsAndBothSidesConfigureFiniteHttp2Keepalive()
    {
        string root = RepoRoot();
        string contracts = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/GrpcHttp2KeepAlive.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktopHandler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", contracts, StringComparison.Ordinal);
        Assert.Contains("PingDelay = TimeSpan.FromSeconds(60)", contracts, StringComparison.Ordinal);
        Assert.Contains("PingTimeout = TimeSpan.FromSeconds(30)", contracts, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromSeconds(60), Mfc.Contracts.GrpcHttp2KeepAlive.PingDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), Mfc.Contracts.GrpcHttp2KeepAlive.PingTimeout);

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingTimeout = GrpcHttp2KeepAlive.PingTimeout", program, StringComparison.Ordinal);
        Assert.Contains("Limits.Http2.KeepAlivePingDelay", program, StringComparison.Ordinal);

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingTimeout = GrpcHttp2KeepAlive.PingTimeout", desktopHandler, StringComparison.Ordinal);

        // Do not regress MSGSIZE / BODY / health / metrics / tracing / resource.
        Assert.Contains("CTRL-KESTREL-BODY-01", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", program, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MaxSendMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("AddGrpcHealthChecks", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsAndQueueLockHttp2KeepaliveIntervals()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string controllerConfig = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan49 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-49-controller-grpc-http2-keepalive.md"));

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", installation, StringComparison.Ordinal);
        Assert.Contains("GrpcHttp2KeepAlive", installation, StringComparison.Ordinal);
        Assert.Contains("60s", installation, StringComparison.Ordinal);
        Assert.Contains("30s", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("GrpcHttp2KeepAlive", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("PingDelay = 60s", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("PingTimeout = 30s", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("never", controllerConfig, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlGrpcKeepalive01ControllerDesktopHttp2KeepaliveLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-342 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-342 | [#1090](https://github.com/sesquicadaver/MTDirector/issues/1090) | CTRL-GRPC-KEEPALIVE-01 — Finite HTTP/2 keepalive for Controller+Desktop Watch streams | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-349 (#1104)", roadmap, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", plan49, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-342)", plan49, StringComparison.Ordinal);
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
