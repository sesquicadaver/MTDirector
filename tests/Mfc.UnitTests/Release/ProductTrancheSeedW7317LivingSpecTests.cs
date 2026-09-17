using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-317: known-limitations / queue seed locked CTRL-HTTP-METRICS-01 (W7-318) after PLAN-43 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7317LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlHttpMetrics01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan43 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-43-controller-http-metrics-otel.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Intentional residual (W7-317 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-318", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-319", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-318**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-317 | [#1040](https://github.com/sesquicadaver/MTDirector/issues/1040) | Seed first PLAN-43 atomic row after inventory → CTRL-HTTP-METRICS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-318 | [#1042](https://github.com/sesquicadaver/MTDirector/issues/1042) | CTRL-HTTP-METRICS-01 — Controller scrapeable Prometheus/OTel metrics beyond HTTP health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-319 | [#1044](https://github.com/sesquicadaver/MTDirector/issues/1044) | Seed next after CTRL-HTTP-METRICS-01 (PLAN-43 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-325 (#1056)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-317", plan, StringComparison.Ordinal);
        Assert.Contains("W7-318", plan, StringComparison.Ordinal);
        Assert.Contains("W7-319", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-325 (#1056)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-317 (#1040) DONE", plan43, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-METRICS-01", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-318", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-319", plan43, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-325 (#1056)", plan43, StringComparison.Ordinal);

        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
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
