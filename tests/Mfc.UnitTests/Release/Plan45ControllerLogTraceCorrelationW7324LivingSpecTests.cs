using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-324: PLAN-45 inventory documents sole CTRL-LOG-OTEL-CORRELATE-01 rank
/// (JSON console log↔trace correlation after OTel tracing) and opens CORRELATE implement after seed.
/// </summary>
public sealed class Plan45ControllerLogTraceCorrelationW7324LivingSpecTests
{
    [Fact]
    public void Ac1Plan45InventoryDocumentsSoleCtrlLogOtelCorrelate01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan45 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-45-controller-log-trace-correlation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string logger = File.ReadAllText(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs"));

        Assert.Contains("PLAN-45 — Controller log↔trace correlation after OpenTelemetry tracing", plan45, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan45, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", plan45, StringComparison.Ordinal);
        Assert.Contains("2da9d508", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-326", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-325", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-324", plan45, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan45, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activity.Current", plan45, StringComparison.Ordinal);
        Assert.Contains("traceId", plan45, StringComparison.Ordinal);
        Assert.Contains("spanId", plan45, StringComparison.Ordinal);
        Assert.Contains("opt-in", plan45, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("§3.C NEXT = W7-335 (#1076)", plan45, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-324 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-326", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-325", limitations, StringComparison.Ordinal);
        Assert.Contains("2da9d508", limitations, StringComparison.Ordinal);

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
        Assert.Contains(
            "W7-327 | [#1060](https://github.com/sesquicadaver/MTDirector/issues/1060) | Seed next after CTRL-LOG-OTEL-CORRELATE-01 (PLAN-45 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-328 | [#1063](https://github.com/sesquicadaver/MTDirector/issues/1063) | PLAN-46 — Inventory Controller OpenTelemetry resource identity after log↔trace correlation | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-329 | [#1064](https://github.com/sesquicadaver/MTDirector/issues/1064) | Seed first PLAN-46 atomic row after inventory → CTRL-HTTP-OTEL-RESOURCE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-330 | [#1066](https://github.com/sesquicadaver/MTDirector/issues/1066) | CTRL-HTTP-OTEL-RESOURCE-01 — Controller OTel ResourceBuilder service.name/service.version | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-331 | [#1068](https://github.com/sesquicadaver/MTDirector/issues/1068) | Seed next after CTRL-HTTP-OTEL-RESOURCE-01 (PLAN-46 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-332 | [#1071](https://github.com/sesquicadaver/MTDirector/issues/1071) | PLAN-47 — Inventory Controller gRPC message-size / transport limits after OTel resource identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-333 | [#1072](https://github.com/sesquicadaver/MTDirector/issues/1072) | Seed first PLAN-47 atomic row after inventory → CTRL-GRPC-MSGSIZE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-334 | [#1074](https://github.com/sesquicadaver/MTDirector/issues/1074) | CTRL-GRPC-MSGSIZE-01 — Align Controller+Desktop gRPC MaxReceive/SendMessageSize with snapshot bounds | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-335 | [#1076](https://github.com/sesquicadaver/MTDirector/issues/1076) | Seed next after CTRL-GRPC-MSGSIZE-01 (PLAN-47 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-335 (#1076)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-325", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-326", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-45", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-45-controller-log-trace-correlation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-45-controller-log-trace-correlation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan45ControllerLogTraceCorrelationW7324", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement correlation yet).
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("/metrics", installation, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("/health/live", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("Activity.Current", logger, StringComparison.Ordinal);
        Assert.Contains("traceId", logger, StringComparison.Ordinal);
        Assert.Contains("/health/live", packagingDoc, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs")));
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
