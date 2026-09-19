using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-347: PLAN-50 COMPLETE; known-limitations / queue seed locked PLAN-51 inventory (W7-348)
/// and follow-up seed W7-349 after CTRL-KESTREL-MINRATE-01.
/// </summary>
public sealed class ProductTrancheSeedW7347LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan51AfterPlan50Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan50 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-50-controller-kestrel-min-data-rate.md"));
        string plan51 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-51-desktop-grpc-unary-deadline.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Intentional residual (W7-347 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-50 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-51", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-348", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-349", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-348**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-347 | [#1099](https://github.com/sesquicadaver/MTDirector/issues/1099) | Seed next after CTRL-KESTREL-MINRATE-01 (PLAN-50 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-348 | [#1103](https://github.com/sesquicadaver/MTDirector/issues/1103) | PLAN-51 — Inventory Desktop gRPC unary call deadline / timeout policy | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-349 | [#1104](https://github.com/sesquicadaver/MTDirector/issues/1104) | Seed first PLAN-51 atomic row after inventory → DESK-GRPC-DEADLINE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-356 (#1119)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-50 COMPLETE", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-347 (#1099) DONE", plan50, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-356 (#1119)", plan50, StringComparison.Ordinal);
        Assert.Contains("plan-51-desktop-grpc-unary-deadline.md", plan50, StringComparison.Ordinal);

        Assert.Contains("PLAN-51", plan, StringComparison.Ordinal);
        Assert.Contains("W7-348", plan, StringComparison.Ordinal);
        Assert.Contains("W7-347 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-356 (#1119)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-51-desktop-grpc-unary-deadline.md", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-GRPC-DEADLINE-01", plan51, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-348", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-349", plan51, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-356 (#1119)", plan51, StringComparison.Ordinal);
        Assert.Contains("Deadline", plan51, StringComparison.Ordinal);

        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
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
