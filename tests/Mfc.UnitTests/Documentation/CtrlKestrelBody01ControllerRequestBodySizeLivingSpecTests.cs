using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-KESTREL-BODY-01: Controller Kestrel MaxRequestBodySize via shared
/// GrpcTransportLimits.MaxMessageBytes (256 MiB). Do not regress MSGSIZE/health/metrics.
/// </summary>
public sealed class CtrlKestrelBody01ControllerRequestBodySizeLivingSpecTests
{
    [Fact]
    public void Ac1KestrelMaxRequestBodySizeAlignedWithGrpcTransportLimits()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string contracts = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/GrpcTransportLimits.cs"));

        Assert.Contains("CTRL-KESTREL-BODY-01", program, StringComparison.Ordinal);
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxRequestBodySize = null", program, StringComparison.Ordinal);
        Assert.Equal(268435456, Mfc.Contracts.GrpcTransportLimits.MaxMessageBytes);
        Assert.Contains("MaxMessageBytes = 256 * 1024 * 1024", contracts, StringComparison.Ordinal);

        // Do not regress MSGSIZE / health / metrics / tracing / resource.
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
    public void Ac2DocsAndQueueLockKestrelBodyLimit()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string controllerConfig = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan48 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-48-controller-kestrel-request-body-limits.md"));

        Assert.Contains("CTRL-KESTREL-BODY-01", installation, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize", installation, StringComparison.Ordinal);
        Assert.Contains("GrpcTransportLimits.MaxMessageBytes", installation, StringComparison.Ordinal);
        Assert.Contains("256 MiB", installation, StringComparison.Ordinal);
        Assert.Contains("268435456", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);

        Assert.Contains("CTRL-KESTREL-BODY-01", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("GrpcTransportLimits.MaxMessageBytes", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("268435456", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("never unlimited", controllerConfig, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("CTRL-KESTREL-BODY-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlKestrelBody01ControllerRequestBodySizeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-338 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-338 | [#1082](https://github.com/sesquicadaver/MTDirector/issues/1082) | CTRL-KESTREL-BODY-01 — Align Kestrel MaxRequestBodySize with GrpcTransportLimits (256 MiB) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", roadmap, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", plan48, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-338)", plan48, StringComparison.Ordinal);
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
