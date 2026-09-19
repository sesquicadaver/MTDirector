using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-365: known-limitations / queue seed locked SNAP-FAULT-CORR-01 (W7-366) after PLAN-55 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7365LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedSnapFaultCorr01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan55 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-55-capture-progress-fault-correlation.md"));
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-365 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-366", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-367", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-366**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-365 | [#1136](https://github.com/sesquicadaver/MTDirector/issues/1136) | Seed first PLAN-55 atomic row after inventory → SNAP-FAULT-CORR-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-366 | [#1138](https://github.com/sesquicadaver/MTDirector/issues/1138) | SNAP-FAULT-CORR-01 — Share capture progress correlation id with RPC fault | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-367 | [#1139](https://github.com/sesquicadaver/MTDirector/issues/1139) | Seed next after SNAP-FAULT-CORR-01 (PLAN-55 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-383 (#1171)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-365", plan, StringComparison.Ordinal);
        Assert.Contains("W7-366", plan, StringComparison.Ordinal);
        Assert.Contains("W7-367", plan, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-383 (#1171)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-365 (#1136) DONE", plan55, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-366", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-367", plan55, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-383 (#1171)", plan55, StringComparison.Ordinal);

        Assert.Equal(0, Count(snapshot, "CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid())"));
        Assert.Equal(2, Count(snapshot, "ToRpcException(result.Error!, sharedId)"));
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}", mapper, StringComparison.Ordinal);
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
