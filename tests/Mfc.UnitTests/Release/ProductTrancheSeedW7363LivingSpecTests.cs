using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-363: PLAN-54 COMPLETE; known-limitations / queue seed locked PLAN-55 inventory (W7-364)
/// and follow-up seed W7-365 after DESK-CONN-FAULT-01.
/// </summary>
public sealed class ProductTrancheSeedW7363LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan55AfterPlan54Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan54 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-54-desktop-connection-status-fault-text.md"));
        string plan55 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-55-capture-progress-fault-correlation.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-363 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-54 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-55", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-364", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-365", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-364**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-363 | [#1131](https://github.com/sesquicadaver/MTDirector/issues/1131) | Seed next after DESK-CONN-FAULT-01 (PLAN-54 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-364 | [#1135](https://github.com/sesquicadaver/MTDirector/issues/1135) | PLAN-55 — Inventory capture progress fault correlation | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-365 | [#1136](https://github.com/sesquicadaver/MTDirector/issues/1136) | Seed first PLAN-55 atomic row after inventory → SNAP-FAULT-CORR-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-54 COMPLETE", plan54, StringComparison.Ordinal);
        Assert.Contains("W7-363 (#1131) DONE", plan54, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan54, StringComparison.Ordinal);
        Assert.Contains("plan-55-capture-progress-fault-correlation.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-55-capture-progress-fault-correlation.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-55", plan, StringComparison.Ordinal);
        Assert.Contains("W7-364", plan, StringComparison.Ordinal);
        Assert.Contains("W7-363 (#1131) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", plan55, StringComparison.Ordinal);
        Assert.Contains("Guid.NewGuid", plan55, StringComparison.Ordinal);
        Assert.Contains("d848a58c", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-364", plan55, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan55, StringComparison.Ordinal);

        Assert.Equal(0, Count(snapshot, "CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid())"));
        Assert.Contains("ToRpcException(result.Error!, sharedId)", snapshot, StringComparison.Ordinal);
        Assert.Contains("(correlation ", viewer, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText.Format(ex)", connection, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code}", mapper, StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + value.Length;
        }
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
