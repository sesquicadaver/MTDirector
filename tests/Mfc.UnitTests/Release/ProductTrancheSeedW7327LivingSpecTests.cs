using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-327: PLAN-45 COMPLETE; known-limitations / queue seed locked PLAN-46 inventory (W7-328)
/// and follow-up seed W7-329 after CTRL-LOG-OTEL-CORRELATE-01.
/// </summary>
public sealed class ProductTrancheSeedW7327LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan46AfterPlan45Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan45 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-45-controller-log-trace-correlation.md"));
        string plan46 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-46-controller-otel-resource-identity.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string logger = File.ReadAllText(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs"));

        Assert.Contains("Intentional residual (W7-327 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-45 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-46", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-328", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-329", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-328**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-327 | [#1060](https://github.com/sesquicadaver/MTDirector/issues/1060) | Seed next after CTRL-LOG-OTEL-CORRELATE-01 (PLAN-45 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-328 | [#1063](https://github.com/sesquicadaver/MTDirector/issues/1063) | PLAN-46 — Inventory Controller OpenTelemetry resource identity after log↔trace correlation | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-329 | [#1064](https://github.com/sesquicadaver/MTDirector/issues/1064) | Seed first PLAN-46 atomic row after inventory → CTRL-HTTP-OTEL-RESOURCE-01 | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-45 COMPLETE", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-327 (#1060) DONE", plan45, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", plan45, StringComparison.Ordinal);
        Assert.Contains("plan-46-controller-otel-resource-identity.md", plan45, StringComparison.Ordinal);

        Assert.Contains("PLAN-46", plan, StringComparison.Ordinal);
        Assert.Contains("W7-328", plan, StringComparison.Ordinal);
        Assert.Contains("W7-327 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-46-controller-otel-resource-identity.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", plan46, StringComparison.Ordinal);
        Assert.Contains("Inventory **OPEN**", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-328", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-329", plan46, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-328 (#1063)", plan46, StringComparison.Ordinal);
        Assert.Contains("f5d51d57", plan46, StringComparison.Ordinal);

        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("Activity.Current", logger, StringComparison.Ordinal);
        Assert.Contains("traceId", logger, StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceBuilder", program, StringComparison.Ordinal);
        Assert.DoesNotContain("service.name", program, StringComparison.Ordinal);
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
