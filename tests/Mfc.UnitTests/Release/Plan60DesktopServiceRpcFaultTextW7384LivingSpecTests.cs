using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-384: PLAN-60 inventory documents sole DESK-SVC-FAULT-01 rank
/// (service Error = ex.Message still swallows RpcException while ViewModels already use DesktopRpcFaultText)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan60DesktopServiceRpcFaultTextW7384LivingSpecTests
{
    [Fact]
    public void Ac1Plan60InventoryDocumentsSoleDeskSvcFault01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan60 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-60-desktop-service-rpc-fault-text.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("PLAN-60 — Desktop service RPC fault text after snapshot ErrorText correlation", plan60, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan60, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", plan60, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan60, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10adc5ba", plan60, StringComparison.Ordinal);
        Assert.Contains("68302273", plan60, StringComparison.Ordinal);
        Assert.Contains("ex.Message", plan60, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText.Format", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-384", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-385", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-386", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-387", plan60, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", plan60, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-384 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-386", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-385", limitations, StringComparison.Ordinal);
        Assert.Contains("68302273", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-384 | [#1175](https://github.com/sesquicadaver/MTDirector/issues/1175) | PLAN-60 — Inventory Desktop service RPC fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-385", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-386", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-60", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-60-desktop-service-rpc-fault-text.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-60-desktop-service-rpc-fault-text.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan60DesktopServiceRpcFaultTextW7384", testing, StringComparison.Ordinal);

        Assert.Equal(3, Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(2, Count(diff, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(1, Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(0, Count(viewerService, "Error = ex.Message"));
        Assert.Equal(0, Count(diff, "Error = ex.Message"));
        Assert.Equal(0, Count(inventory, "Error = ex.Message"));
        Assert.Contains("ErrorText = FormatCaptureProgress(failed)", viewer, StringComparison.Ordinal);
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex);", viewer, StringComparison.Ordinal);
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
