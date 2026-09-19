using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-315: PLAN-42 COMPLETE; known-limitations / queue seed locked PLAN-43 inventory (W7-316)
/// and follow-up seed W7-317 after CTRL-HTTP-HEALTH-01.
/// </summary>
public sealed class ProductTrancheSeedW7315LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan43AfterPlan42Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan42 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-42-controller-http-health-probes.md"));
        string plan43 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-43-controller-http-metrics-otel.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string healthCheck = Path.Combine(root, "src/Mfc.Controller/DatabaseReadyHealthCheck.cs");

        Assert.Contains("Intentional residual (W7-315 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-42 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-43", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-316", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-317", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-316**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-315 | [#1036](https://github.com/sesquicadaver/MTDirector/issues/1036) | Seed next after CTRL-HTTP-HEALTH-01 (PLAN-42 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-370 (#1146)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-42 COMPLETE", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-315 (#1036) DONE", plan42, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-370 (#1146)", plan42, StringComparison.Ordinal);
        Assert.Contains("plan-43-controller-http-metrics-otel.md", plan42, StringComparison.Ordinal);

        Assert.Contains("PLAN-43", plan, StringComparison.Ordinal);
        Assert.Contains("W7-316", plan, StringComparison.Ordinal);
        Assert.Contains("W7-315 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-370 (#1146)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-43-controller-http-metrics-otel.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-METRICS-01", plan43, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-316", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-317", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-318", plan43, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-370 (#1146)", plan43, StringComparison.Ordinal);
        Assert.Contains("94f04744", plan43, StringComparison.Ordinal);

        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("/health/live", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.True(File.Exists(healthCheck));
        // Import order locked by W7-315 format fix (Mfc.* before Microsoft.*).
        string healthText = File.ReadAllText(healthCheck);
        int mfc = healthText.IndexOf("using Mfc.Infrastructure.Persistence;", StringComparison.Ordinal);
        int ms = healthText.IndexOf("using Microsoft.Extensions.Diagnostics.HealthChecks;", StringComparison.Ordinal);
        Assert.True(mfc >= 0 && ms > mfc);
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
