using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-HTTP-OTEL-RESOURCE-01: OTel ResourceBuilder service.name/version/instance.id
/// alongside health/metrics/tracing/log correlation (opt-in fail-closed unchanged).
/// </summary>
public sealed class CtrlHttpOtelResource01ControllerResourceLivingSpecTests
{
    [Fact]
    public void Ac1ProgramConfiguresOtelResourceIdentityAlongsideOptInSignals()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", program, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("ResourceBuilder", program, StringComparison.Ordinal);
        Assert.Contains("AddService", program, StringComparison.Ordinal);
        Assert.Contains("Mfc.Controller", program, StringComparison.Ordinal);
        Assert.Contains("ResolveControllerServiceVersion", program, StringComparison.Ordinal);
        Assert.Contains("Environment.MachineName", program, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Resources", program, StringComparison.Ordinal);

        // Do not regress health / metrics / tracing / correlation opt-in.
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("tracingOptions.Enabled", program, StringComparison.Ordinal);
        Assert.Contains("metricsOptions.Enabled", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsDocumentOtelResourceIdentity()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan46 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-46-controller-otel-resource-identity.md"));

        Assert.Contains("service.name", installation, StringComparison.Ordinal);
        Assert.Contains("service.version", installation, StringComparison.Ordinal);
        Assert.Contains("service.instance.id", installation, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);

        Assert.Contains("service.name", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("service.version", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", packagingDoc, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlHttpOtelResource01ControllerResourceLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-330 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-330 | [#1066](https://github.com/sesquicadaver/MTDirector/issues/1066) | CTRL-HTTP-OTEL-RESOURCE-01 — Controller OTel ResourceBuilder service.name/service.version | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-373 (#1152)", roadmap, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", plan46, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-330)", plan46, StringComparison.Ordinal);
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
