using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-319: PLAN-43 COMPLETE; known-limitations / queue seed locked PLAN-44 inventory (W7-320)
/// and follow-up seed W7-321 after CTRL-HTTP-METRICS-01.
/// </summary>
public sealed class ProductTrancheSeedW7319LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan44AfterPlan43Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan43 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-43-controller-http-metrics-otel.md"));
        string plan44 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-44-controller-otel-tracing.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Intentional residual (W7-319 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-43 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-44", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-320", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-321", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-320**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-319 | [#1044](https://github.com/sesquicadaver/MTDirector/issues/1044) | Seed next after CTRL-HTTP-METRICS-01 (PLAN-43 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-43 COMPLETE", plan43, StringComparison.Ordinal);
        Assert.Contains("W7-319 (#1044) DONE", plan43, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan43, StringComparison.Ordinal);
        Assert.Contains("plan-44-controller-otel-tracing.md", plan43, StringComparison.Ordinal);

        Assert.Contains("PLAN-44", plan, StringComparison.Ordinal);
        Assert.Contains("W7-320", plan, StringComparison.Ordinal);
        Assert.Contains("W7-319 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-44-controller-otel-tracing.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", plan44, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-320", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-321", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-322", plan44, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan44, StringComparison.Ordinal);
        Assert.Contains("0929ef8d", plan44, StringComparison.Ordinal);

        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("/metrics", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
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
