using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-373: known-limitations / queue seed locked DESK-VRRP-PROG-01 (W7-374) after PLAN-57 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7373LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskVrrpProg01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan57 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-57-vrrp-capture-progress-fault-text.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-373 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-374", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-375", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-374**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-373 | [#1152](https://github.com/sesquicadaver/MTDirector/issues/1152) | Seed first PLAN-57 atomic row after inventory → DESK-VRRP-PROG-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-374 | [#1154](https://github.com/sesquicadaver/MTDirector/issues/1154) | DESK-VRRP-PROG-01 — Show capture-progress correlation id on VRRP pair status | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-375 | [#1155](https://github.com/sesquicadaver/MTDirector/issues/1155) | Seed next after DESK-VRRP-PROG-01 (PLAN-57 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-373", plan, StringComparison.Ordinal);
        Assert.Contains("W7-374", plan, StringComparison.Ordinal);
        Assert.Contains("W7-375", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-373 (#1152) DONE", plan57, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-374", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-375", plan57, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", plan57, StringComparison.Ordinal);

        Assert.Contains("VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\"", node, StringComparison.Ordinal);
        Assert.DoesNotContain("progress.Error", node, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
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
