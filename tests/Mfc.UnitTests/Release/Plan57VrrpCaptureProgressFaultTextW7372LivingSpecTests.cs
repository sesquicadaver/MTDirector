using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-372: PLAN-57 inventory documents sole DESK-VRRP-PROG-01 rank
/// (Watch writes stage-only pair status and the incomplete-capture throw drops the progress correlation id)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan57VrrpCaptureProgressFaultTextW7372LivingSpecTests
{
    [Fact]
    public void Ac1Plan57InventoryDocumentsSoleDeskVrrpProg01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan57 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-57-vrrp-capture-progress-fault-text.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("PLAN-57 — VRRP capture-progress fault text after pair-status fault text", plan57, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan57, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", plan57, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan57, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("218cdba7", plan57, StringComparison.Ordinal);
        Assert.Contains("7dbfe132", plan57, StringComparison.Ordinal);
        Assert.Contains("progress.Stage", plan57, StringComparison.Ordinal);
        Assert.Contains("progress.Error", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-372", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-373", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-374", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-375", plan57, StringComparison.Ordinal);
        Assert.Contains("FormatCaptureProgress", plan57, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-376 (#1159)", plan57, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-372 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-374", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-373", limitations, StringComparison.Ordinal);
        Assert.Contains("7dbfe132", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-372 | [#1151](https://github.com/sesquicadaver/MTDirector/issues/1151) | PLAN-57 — Inventory VRRP capture-progress fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-376 (#1159)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-373", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-374", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-57", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-57-vrrp-capture-progress-fault-text.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-57-vrrp-capture-progress-fault-text.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan57VrrpCaptureProgressFaultTextW7372", testing, StringComparison.Ordinal);

        Assert.Equal(1, Count(node, "VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\""));
        Assert.DoesNotContain("progress.Error", node, StringComparison.Ordinal);
        Assert.Contains(
            "Node capture did not complete successfully for all VRRP members.",
            node,
            StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
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
