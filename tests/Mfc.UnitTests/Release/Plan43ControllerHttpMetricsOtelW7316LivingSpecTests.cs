using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-316: PLAN-43 inventory documents sole CTRL-HTTP-METRICS-01 rank
/// (metrics/OTel beyond HTTP health) and opens METRICS implement after seed.
/// </summary>
public sealed class Plan43ControllerHttpMetricsOtelW7316LivingSpecTests
{
    [Fact]
    public void Ac1Plan43InventoryDocumentsSoleCtrlHttpMetrics01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan43 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-43-controller-http-metrics-otel.md"));
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

        Assert.Contains("PLAN-43 — Controller metrics / OpenTelemetry beyond HTTP health probes", plan43, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan43, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01", plan43, StringComparison.Ordinal);
        Assert.Contains("94f04744", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-318", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-317", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-316", plan43, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan43, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MapPrometheusScrapingEndpoint", plan43, StringComparison.Ordinal);
        Assert.Contains("/metrics", plan43, StringComparison.Ordinal);
        Assert.Contains("opt-in", plan43, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("§3.C NEXT = W7-392 (#1191)", plan43, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", plan43, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-316 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-318", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-317", limitations, StringComparison.Ordinal);
        Assert.Contains("94f04744", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-316 | [#1039](https://github.com/sesquicadaver/MTDirector/issues/1039) | PLAN-43 — Inventory Controller metrics/OpenTelemetry beyond HTTP health probes | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-317 | [#1040](https://github.com/sesquicadaver/MTDirector/issues/1040) | Seed first PLAN-43 atomic row after inventory → CTRL-HTTP-METRICS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-318 | [#1042](https://github.com/sesquicadaver/MTDirector/issues/1042) | CTRL-HTTP-METRICS-01 — Controller scrapeable Prometheus/OTel metrics beyond HTTP health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-392 (#1191)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-317", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-318", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-43", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-43-controller-http-metrics-otel.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-43-controller-http-metrics-otel.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan43ControllerHttpMetricsOtelW7316", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement metrics yet).
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("/health/live", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry", csproj, StringComparison.Ordinal);
        Assert.Contains("OpenTelemetry", packages, StringComparison.Ordinal);
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
