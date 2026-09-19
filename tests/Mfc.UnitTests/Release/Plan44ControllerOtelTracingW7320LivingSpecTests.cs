using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-320: PLAN-44 inventory documents sole CTRL-HTTP-OTEL-TRACE-01 rank
/// (OTel tracing beyond metrics scrape) and opens TRACE implement after seed.
/// </summary>
public sealed class Plan44ControllerOtelTracingW7320LivingSpecTests
{
    [Fact]
    public void Ac1Plan44InventoryDocumentsSoleCtrlHttpOtelTrace01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan44 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-44-controller-otel-tracing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string csproj = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Mfc.Controller.csproj"));
        string packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));

        Assert.Contains("PLAN-44 — Controller OpenTelemetry tracing beyond metrics scrape", plan44, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan44, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", plan44, StringComparison.Ordinal);
        Assert.Contains("0929ef8d", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-322", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-321", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-320", plan44, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan44, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WithTracing", plan44, StringComparison.Ordinal);
        Assert.Contains("OTLP", plan44, StringComparison.Ordinal);
        Assert.Contains("ConsoleExporter", plan44, StringComparison.Ordinal);
        Assert.Contains("Mfc:Tracing:Enabled", plan44, StringComparison.Ordinal);
        Assert.Contains("opt-in", plan44, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", plan44, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", plan44, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-320 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-322", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-321", limitations, StringComparison.Ordinal);
        Assert.Contains("0929ef8d", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-320 | [#1047](https://github.com/sesquicadaver/MTDirector/issues/1047) | PLAN-44 — Inventory Controller OpenTelemetry tracing beyond metrics scrape | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-321 | [#1048](https://github.com/sesquicadaver/MTDirector/issues/1048) | Seed first PLAN-44 atomic row after inventory → CTRL-HTTP-OTEL-TRACE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-322 | [#1050](https://github.com/sesquicadaver/MTDirector/issues/1050) | CTRL-HTTP-OTEL-TRACE-01 — Controller opt-in OpenTelemetry tracing beyond metrics scrape | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-321", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-322", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-44", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-44-controller-otel-tracing.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-44-controller-otel-tracing.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan44ControllerOtelTracingW7320", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement tracing yet).
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("/metrics", installation, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("/health/live", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry", csproj, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry", packages, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry.Exporter.OpenTelemetryProtocol", packages, StringComparison.Ordinal);
        Assert.Contains("/health/live", packagingDoc, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "src/Mfc.Controller/Program.cs")));
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
