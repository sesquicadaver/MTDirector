using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-KESTREL-MINRATE-01: Controller Kestrel MinRequestBodyDataRate /
/// MinResponseDataRate disabled (null) for quiet Watch streams.
/// Do not regress keepalive/MSGSIZE/BODY/health/metrics/tracing.
/// </summary>
public sealed class CtrlKestrelMinrate01ControllerMinDataRateLivingSpecTests
{
    [Fact]
    public void Ac1KestrelMinRequestAndResponseDataRatesDisabled()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("CTRL-KESTREL-MINRATE-01", program, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("Limits.MinRequestBodyDataRate", program, StringComparison.Ordinal);
        Assert.Contains("Limits.MinResponseDataRate", program, StringComparison.Ordinal);

        // Do not regress MSGSIZE / BODY / KEEPALIVE / health / metrics / tracing / resource.
        Assert.Contains("CTRL-KESTREL-BODY-01", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingTimeout = GrpcHttp2KeepAlive.PingTimeout", program, StringComparison.Ordinal);
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
    public void Ac2DocsAndQueueLockNullMinDataRatePolicy()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string controllerConfig = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan50 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-50-controller-kestrel-min-data-rate.md"));

        Assert.Contains("CTRL-KESTREL-MINRATE-01", installation, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate", installation, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate", installation, StringComparison.Ordinal);
        Assert.Contains("null", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);

        Assert.Contains("CTRL-KESTREL-MINRATE-01", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("null", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("240 B/s", controllerConfig, StringComparison.Ordinal);

        Assert.Contains("CTRL-KESTREL-MINRATE-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlKestrelMinrate01ControllerMinDataRateLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-346 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-346 | [#1098](https://github.com/sesquicadaver/MTDirector/issues/1098) | CTRL-KESTREL-MINRATE-01 — Disable Kestrel MinRequest/ResponseDataRate for quiet Watch streams | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-353 (#1112)", roadmap, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", plan50, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-346)", plan50, StringComparison.Ordinal);
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
