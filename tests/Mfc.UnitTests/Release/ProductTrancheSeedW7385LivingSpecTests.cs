using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-385: known-limitations / queue seed locked DESK-SVC-FAULT-01 (W7-386) after PLAN-60 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7385LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskSvcFault01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan60 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-60-desktop-service-rpc-fault-text.md"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-385 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-386", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-387", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-386**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-385 | [#1176](https://github.com/sesquicadaver/MTDirector/issues/1176) | Seed first PLAN-60 atomic row after inventory → DESK-SVC-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-386 | [#1178](https://github.com/sesquicadaver/MTDirector/issues/1178) | DESK-SVC-FAULT-01 — Store DesktopRpcFaultText.Format on service Error for RpcException | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-387 | [#1179](https://github.com/sesquicadaver/MTDirector/issues/1179) | Seed next after DESK-SVC-FAULT-01 (PLAN-60 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-385", plan, StringComparison.Ordinal);
        Assert.Contains("W7-386", plan, StringComparison.Ordinal);
        Assert.Contains("W7-387", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-385 (#1176) DONE", plan60, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-386", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-387", plan60, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", plan60, StringComparison.Ordinal);

        Assert.Equal(
            6,
            Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(diff, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(
            0,
            Count(viewerService, "Error = ex.Message")
            + Count(diff, "Error = ex.Message")
            + Count(inventory, "Error = ex.Message"));
        Assert.Contains("ErrorText = FormatCaptureProgress(failed)", viewer, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
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
