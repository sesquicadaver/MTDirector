using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-323: PLAN-44 COMPLETE; known-limitations / queue seed locked PLAN-45 inventory (W7-324)
/// and follow-up seed W7-325 after CTRL-HTTP-OTEL-TRACE-01.
/// </summary>
public sealed class ProductTrancheSeedW7323LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan45AfterPlan44Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan44 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-44-controller-otel-tracing.md"));
        string plan45 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-45-controller-log-trace-correlation.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string logger = File.ReadAllText(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs"));

        Assert.Contains("Intentional residual (W7-323 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-44 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-45", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-324", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-325", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-324**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-323 | [#1052](https://github.com/sesquicadaver/MTDirector/issues/1052) | Seed next after CTRL-HTTP-OTEL-TRACE-01 (PLAN-44 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-324 | [#1055](https://github.com/sesquicadaver/MTDirector/issues/1055) | PLAN-45 — Inventory Controller log↔trace correlation after OTel tracing | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-325 | [#1056](https://github.com/sesquicadaver/MTDirector/issues/1056) | Seed first PLAN-45 atomic row after inventory → CTRL-LOG-OTEL-CORRELATE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-326 | [#1058](https://github.com/sesquicadaver/MTDirector/issues/1058) | CTRL-LOG-OTEL-CORRELATE-01 — Enrich JSON console logs with Activity TraceId/SpanId | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-44 COMPLETE", plan44, StringComparison.Ordinal);
        Assert.Contains("W7-323 (#1052) DONE", plan44, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", plan44, StringComparison.Ordinal);
        Assert.Contains("plan-45-controller-log-trace-correlation.md", plan44, StringComparison.Ordinal);

        Assert.Contains("PLAN-45", plan, StringComparison.Ordinal);
        Assert.Contains("W7-324", plan, StringComparison.Ordinal);
        Assert.Contains("W7-323 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-45-controller-log-trace-correlation.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", plan45, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-324", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-325", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-326", plan45, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", plan45, StringComparison.Ordinal);
        Assert.Contains("2da9d508", plan45, StringComparison.Ordinal);

        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("Activity.Current", logger, StringComparison.Ordinal);
        Assert.Contains("traceId", logger, StringComparison.Ordinal);
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
