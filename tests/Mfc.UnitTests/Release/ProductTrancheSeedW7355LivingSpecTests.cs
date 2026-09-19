using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-355: PLAN-52 COMPLETE; known-limitations / queue seed locked PLAN-53 inventory (W7-356)
/// and follow-up seed W7-357 after DESK-RPC-FAULT-01.
/// </summary>
public sealed class ProductTrancheSeedW7355LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan53AfterPlan52Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan52 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-52-desktop-grpc-error-detail.md"));
        string plan53 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-53-controller-fault-correlation-log.md"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-355 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-52 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-53", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-356", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-357", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-356**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-355 | [#1115](https://github.com/sesquicadaver/MTDirector/issues/1115) | Seed next after DESK-RPC-FAULT-01 (PLAN-52 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-356 | [#1119](https://github.com/sesquicadaver/MTDirector/issues/1119) | PLAN-53 — Inventory Controller fault-correlation logging | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-357 | [#1120](https://github.com/sesquicadaver/MTDirector/issues/1120) | Seed first PLAN-53 atomic row after inventory → CTRL-ERRDETAIL-LOG-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-52 COMPLETE", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-355 (#1115) DONE", plan52, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan52, StringComparison.Ordinal);
        Assert.Contains("plan-53-controller-fault-correlation-log.md", plan, StringComparison.Ordinal);

        Assert.Contains("PLAN-53", plan, StringComparison.Ordinal);
        Assert.Contains("W7-356", plan, StringComparison.Ordinal);
        Assert.Contains("W7-355 (#1115) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", plan53, StringComparison.Ordinal);
        Assert.Contains("Guid.NewGuid", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-356", plan53, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan53, StringComparison.Ordinal);

        Assert.Contains("mfc-error-detail-bin", fault, StringComparison.Ordinal);
        Assert.Contains("correlationId ?? Guid.NewGuid()", mapper, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code}", mapper, StringComparison.Ordinal);
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
