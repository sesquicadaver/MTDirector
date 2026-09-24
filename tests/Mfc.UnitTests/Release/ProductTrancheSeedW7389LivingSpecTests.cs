using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-389: known-limitations / queue seed locked DESK-CONN-DISC-01 (W7-390) after PLAN-61 inventory.
/// Docs only — does not change ControllerConnectionService.
/// </summary>
public sealed class ProductTrancheSeedW7389LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskConnDisc01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-389 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-390", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-391", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-390**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-389 | [#1184](https://github.com/sesquicadaver/MTDirector/issues/1184) | Seed first PLAN-61 atomic row after inventory → DESK-CONN-DISC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-390 | [#1186](https://github.com/sesquicadaver/MTDirector/issues/1186) | DESK-CONN-DISC-01 — Store DesktopRpcFaultText.Format when Disconnected catches are RpcException | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-391 | [#1187](https://github.com/sesquicadaver/MTDirector/issues/1187) | Seed next after DESK-CONN-DISC-01 (PLAN-61 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-389", plan, StringComparison.Ordinal);
        Assert.Contains("W7-390", plan, StringComparison.Ordinal);
        Assert.Contains("W7-391", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-389 (#1184) DONE", plan61, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-390", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-391", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan61, StringComparison.Ordinal);

        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.Disconnected, DesktopRpcFaultText.Format(ex))"));
        Assert.Equal(0, Count(connection, "SetState(ControllerConnectionState.Disconnected, ex.Message)"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
        Assert.Equal(
            6,
            Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(diff, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
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
