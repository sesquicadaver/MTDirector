using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-387: PLAN-60 COMPLETE; known-limitations / queue seed locked PLAN-61 inventory (W7-388)
/// and follow-up seed W7-389 after DESK-SVC-FAULT-01.
/// </summary>
public sealed class ProductTrancheSeedW7387LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan61AfterPlan60Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan60 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-60-desktop-service-rpc-fault-text.md"));
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-387 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-60 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-61", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-388", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-389", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-388**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-387 | [#1179](https://github.com/sesquicadaver/MTDirector/issues/1179) | Seed next after DESK-SVC-FAULT-01 (PLAN-60 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-388 | [#1183](https://github.com/sesquicadaver/MTDirector/issues/1183) | PLAN-61 — Inventory Desktop connection Disconnected RPC fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-389 | [#1184](https://github.com/sesquicadaver/MTDirector/issues/1184) | Seed first PLAN-61 atomic row after inventory → DESK-CONN-DISC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-60 COMPLETE", plan60, StringComparison.Ordinal);
        Assert.Contains("W7-387 (#1179) DONE", plan60, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan60, StringComparison.Ordinal);
        Assert.Contains("plan-61-desktop-connection-disconnected-fault-text.md", plan, StringComparison.Ordinal);
        Assert.Contains("plan-61-desktop-connection-disconnected-fault-text.md", docsIndex, StringComparison.Ordinal);

        Assert.Contains("PLAN-61", plan, StringComparison.Ordinal);
        Assert.Contains("W7-388", plan, StringComparison.Ordinal);
        Assert.Contains("W7-387 (#1179) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", plan61, StringComparison.Ordinal);
        Assert.Contains("ex.Message", plan61, StringComparison.Ordinal);
        Assert.Contains("33682bb6", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-388", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan61, StringComparison.Ordinal);

        Assert.Equal(
            6,
            Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(diff, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.Disconnected, DesktopRpcFaultText.Format(ex))"));
        Assert.Equal(0, Count(connection, "SetState(ControllerConnectionState.Disconnected, ex.Message)"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
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
