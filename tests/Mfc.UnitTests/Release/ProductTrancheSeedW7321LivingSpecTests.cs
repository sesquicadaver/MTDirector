using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-321: known-limitations / queue seed locked CTRL-HTTP-OTEL-TRACE-01 (W7-322) after PLAN-44 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7321LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlHttpOtelTrace01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan44 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-44-controller-otel-tracing.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Intentional residual (W7-321 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-322", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-323", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-322**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-321 | [#1048](https://github.com/sesquicadaver/MTDirector/issues/1048) | Seed first PLAN-44 atomic row after inventory → CTRL-HTTP-OTEL-TRACE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-322 | [#1050](https://github.com/sesquicadaver/MTDirector/issues/1050) | CTRL-HTTP-OTEL-TRACE-01 — Controller opt-in OpenTelemetry tracing beyond metrics scrape | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-323 | [#1052](https://github.com/sesquicadaver/MTDirector/issues/1052) | Seed next after CTRL-HTTP-OTEL-TRACE-01 (PLAN-44 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-333 (#1072)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-321", plan, StringComparison.Ordinal);
        Assert.Contains("W7-322", plan, StringComparison.Ordinal);
        Assert.Contains("W7-323", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-333 (#1072)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-321 (#1048) DONE", plan44, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-TRACE-01", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-322", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-323", plan44, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-333 (#1072)", plan44, StringComparison.Ordinal);

        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
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
