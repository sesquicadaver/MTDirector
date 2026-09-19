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
            "W7-335 | [#1076](https://github.com/sesquicadaver/MTDirector/issues/1076) | Seed next after CTRL-GRPC-MSGSIZE-01 (PLAN-47 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-336 | [#1079](https://github.com/sesquicadaver/MTDirector/issues/1079) | PLAN-48 — Inventory Controller Kestrel request-body / HTTP2 limits after gRPC message-size | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-337 | [#1080](https://github.com/sesquicadaver/MTDirector/issues/1080) | Seed first PLAN-48 atomic row after inventory → CTRL-KESTREL-BODY-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-338 | [#1082](https://github.com/sesquicadaver/MTDirector/issues/1082) | CTRL-KESTREL-BODY-01 — Align Kestrel MaxRequestBodySize with GrpcTransportLimits (256 MiB) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-339 | [#1084](https://github.com/sesquicadaver/MTDirector/issues/1084) | Seed next after CTRL-KESTREL-BODY-01 (PLAN-48 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-340 | [#1087](https://github.com/sesquicadaver/MTDirector/issues/1087) | PLAN-49 — Inventory Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-341 | [#1088](https://github.com/sesquicadaver/MTDirector/issues/1088) | Seed first PLAN-49 atomic row after inventory → CTRL-GRPC-KEEPALIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-369 (#1144)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-45 COMPLETE", plan45, StringComparison.Ordinal);
        Assert.Contains("W7-327 (#1060) DONE", plan45, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-369 (#1144)", plan45, StringComparison.Ordinal);
        Assert.Contains("plan-46-controller-otel-resource-identity.md", plan45, StringComparison.Ordinal);

        Assert.Contains("PLAN-46", plan, StringComparison.Ordinal);
        Assert.Contains("W7-328", plan, StringComparison.Ordinal);
        Assert.Contains("W7-327 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-369 (#1144)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-46-controller-otel-resource-identity.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", plan46, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-328", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-329", plan46, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-369 (#1144)", plan46, StringComparison.Ordinal);
        Assert.Contains("894cc4b8", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-330", plan46, StringComparison.Ordinal);

        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("Activity.Current", logger, StringComparison.Ordinal);
        Assert.Contains("traceId", logger, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("AddService", program, StringComparison.Ordinal);
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
