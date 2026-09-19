using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-HTTP-METRICS-01: opt-in Prometheus /metrics via OpenTelemetry alongside HTTP/gRPC health.
/// </summary>
public sealed class CtrlHttpMetrics01ControllerMetricsLivingSpecTests
{
    [Fact]
    public void Ac1ProgramMapsOptInPrometheusScrapingAlongsideHealth()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Configuration/ControllerOptions.cs"));
        string csproj = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Mfc.Controller.csproj"));
        string packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("AddPrometheusExporter", program, StringComparison.Ordinal);
        Assert.Contains("AddAspNetCoreInstrumentation", program, StringComparison.Ordinal);
        Assert.Contains("AddRuntimeInstrumentation", program, StringComparison.Ordinal);
        Assert.Contains("metricsOptions.Enabled", program, StringComparison.Ordinal);
        Assert.Contains("NormalizeMetricsScrapePath", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program, StringComparison.Ordinal);
        Assert.Contains("MapGrpcHealthChecksService", program, StringComparison.Ordinal);

        Assert.Contains("class MetricsHostOptions", options, StringComparison.Ordinal);
        Assert.Contains("public bool Enabled { get; init; }", options, StringComparison.Ordinal);
        Assert.Contains("ScrapePath", options, StringComparison.Ordinal);
        Assert.Contains("/metrics", options, StringComparison.Ordinal);

        Assert.Contains("OpenTelemetry.Exporter.Prometheus.AspNetCore", csproj, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Extensions.Hosting", csproj, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Exporter.Prometheus.AspNetCore", packages, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Extensions.Hosting", packages, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsDocumentOptInMetricsPath()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string envSample = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.env.example"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));

        Assert.Contains("/metrics", installation, StringComparison.Ordinal);
        Assert.Contains("Mfc:Metrics:Enabled", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("/metrics", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("MFC__Metrics__Enabled", envSample, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-METRICS-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlHttpMetrics01ControllerMetricsLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-318 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-318 | [#1042](https://github.com/sesquicadaver/MTDirector/issues/1042) | CTRL-HTTP-METRICS-01 — Controller scrapeable Prometheus/OTel metrics beyond HTTP health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", roadmap, StringComparison.Ordinal);
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
