using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-367: PLAN-55 COMPLETE; known-limitations / queue seed locked PLAN-56 inventory (W7-368)
/// and follow-up seed W7-369 after SNAP-FAULT-CORR-01.
/// </summary>
public sealed class ProductTrancheSeedW7367LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan56AfterPlan55Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan55 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-55-capture-progress-fault-correlation.md"));
        string plan56 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-56-vrrp-pair-status-fault-text.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-367 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-55 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-56", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-368", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-369", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-368**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-367 | [#1139](https://github.com/sesquicadaver/MTDirector/issues/1139) | Seed next after SNAP-FAULT-CORR-01 (PLAN-55 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-368 | [#1143](https://github.com/sesquicadaver/MTDirector/issues/1143) | PLAN-56 — Inventory VRRP pair status fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-369 | [#1144](https://github.com/sesquicadaver/MTDirector/issues/1144) | Seed first PLAN-56 atomic row after inventory → DESK-VRRP-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-55 COMPLETE", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-367 (#1139) DONE", plan55, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan55, StringComparison.Ordinal);
        Assert.Contains("plan-56-vrrp-pair-status-fault-text.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-56-vrrp-pair-status-fault-text.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-56", plan, StringComparison.Ordinal);
        Assert.Contains("W7-368", plan, StringComparison.Ordinal);
        Assert.Contains("W7-367 (#1139) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", plan56, StringComparison.Ordinal);
        Assert.Contains("VRRP pair consistency failed", plan56, StringComparison.Ordinal);
        Assert.Contains("f5560b4c", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-368", plan56, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan56, StringComparison.Ordinal);

        Assert.Equal(1, Count(node, "VrrpPairStatusText = \"VRRP pair consistency failed.\""));
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\"", node, StringComparison.Ordinal);
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex)", node, StringComparison.Ordinal);
        Assert.DoesNotContain("correlation", node, StringComparison.OrdinalIgnoreCase);
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
