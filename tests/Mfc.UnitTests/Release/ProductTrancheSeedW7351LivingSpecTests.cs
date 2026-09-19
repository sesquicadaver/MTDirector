using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-351: PLAN-51 COMPLETE; known-limitations / queue seed locked PLAN-52 inventory (W7-352)
/// and follow-up seed W7-353 after DESK-GRPC-DEADLINE-01.
/// </summary>
public sealed class ProductTrancheSeedW7351LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan52AfterPlan51Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan51 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-51-desktop-grpc-unary-deadline.md"));
        string plan52 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-52-desktop-grpc-error-detail.md"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-351 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-51 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-52", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-352", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-353", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-352**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-351 | [#1107](https://github.com/sesquicadaver/MTDirector/issues/1107) | Seed next after DESK-GRPC-DEADLINE-01 (PLAN-51 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-352 | [#1111](https://github.com/sesquicadaver/MTDirector/issues/1111) | PLAN-52 — Inventory Desktop gRPC ErrorDetail operator mapping | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-353 | [#1112](https://github.com/sesquicadaver/MTDirector/issues/1112) | Seed first PLAN-52 atomic row after inventory → DESK-RPC-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-51 COMPLETE", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-351 (#1107) DONE", plan51, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan51, StringComparison.Ordinal);
        Assert.Contains("plan-52-desktop-grpc-error-detail.md", plan, StringComparison.Ordinal);

        Assert.Contains("PLAN-52", plan, StringComparison.Ordinal);
        Assert.Contains("W7-352", plan, StringComparison.Ordinal);
        Assert.Contains("W7-351 (#1107) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", plan52, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-352", plan52, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan52, StringComparison.Ordinal);

        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", helper, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
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
