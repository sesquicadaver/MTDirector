using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-HTTP-OTEL-TRACE-01: opt-in OpenTelemetry tracing alongside HTTP/gRPC health + metrics.
/// </summary>
public sealed class CtrlHttpOtelTrace01ControllerTracingLivingSpecTests
{
    [Fact]
    public void Ac1ProgramRegistersOptInWithTracingAlongsideHealthAndMetrics()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Configuration/ControllerOptions.cs"));
        string csproj = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Mfc.Controller.csproj"));
        string packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("AddAspNetCoreInstrumentation", program, StringComparison.Ordinal);
        Assert.Contains("AddOtlpExporter", program, StringComparison.Ordinal);
        Assert.Contains("AddConsoleExporter", program, StringComparison.Ordinal);
        Assert.Contains("tracingOptions.Enabled", program, StringComparison.Ordinal);
        Assert.Contains("ConsoleExporter", program, StringComparison.Ordinal);
        Assert.Contains("OtlpEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program, StringComparison.Ordinal);
        Assert.Contains("MapGrpcHealthChecksService", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);

        Assert.Contains("class TracingHostOptions", options, StringComparison.Ordinal);
        Assert.Contains("public bool Enabled { get; init; }", options, StringComparison.Ordinal);
        Assert.Contains("OtlpEndpoint", options, StringComparison.Ordinal);
        Assert.Contains("ConsoleExporter", options, StringComparison.Ordinal);

        Assert.Contains("OpenTelemetry.Exporter.OpenTelemetryProtocol", csproj, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Exporter.Console", csproj, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Exporter.OpenTelemetryProtocol", packages, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Exporter.Console", packages, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsDocumentOptInTracingExporters()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string envSample = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.env.example"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));

        Assert.Contains("Mfc:Tracing:Enabled", installation, StringComparison.Ordinal);
        Assert.Contains("Mfc:Tracing:OtlpEndpoint", installation, StringComparison.Ordinal);
        Assert.Contains("Mfc:Tracing:ConsoleExporter", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("MFC__Tracing__Enabled", envSample, StringComparison.Ordinal);
        Assert.Contains("Tracing export", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("OtlpEndpoint", packagingDoc, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlHttpOtelTrace01ControllerTracingLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-322 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-322 | [#1050](https://github.com/sesquicadaver/MTDirector/issues/1050) | CTRL-HTTP-OTEL-TRACE-01 — Controller opt-in OpenTelemetry tracing beyond metrics scrape | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-397 (#1200)", roadmap, StringComparison.Ordinal);
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
