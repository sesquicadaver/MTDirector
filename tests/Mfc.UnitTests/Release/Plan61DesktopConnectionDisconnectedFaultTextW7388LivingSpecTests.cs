using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-388: PLAN-61 inventory documents sole DESK-CONN-DISC-01 rank
/// (connect and reconnect Disconnected = ex.Message still swallow RpcException
/// while AuthenticationFailed already uses DesktopRpcFaultText)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan61DesktopConnectionDisconnectedFaultTextW7388LivingSpecTests
{
    [Fact]
    public void Ac1Plan61InventoryDocumentsSoleDeskConnDisc01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("PLAN-61 — Desktop connection Disconnected RPC fault text after service Error correlation", plan61, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan61, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", plan61, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan61, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("33682bb6", plan61, StringComparison.Ordinal);
        Assert.Contains("994863c1", plan61, StringComparison.Ordinal);
        Assert.Contains("ex.Message", plan61, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText.Format", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-388", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-389", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-390", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-391", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", plan61, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-388 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-390", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-389", limitations, StringComparison.Ordinal);
        Assert.Contains("994863c1", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-388 | [#1183](https://github.com/sesquicadaver/MTDirector/issues/1183) | PLAN-61 — Inventory Desktop connection Disconnected RPC fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-389", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-390", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-61", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-61-desktop-connection-disconnected-fault-text.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-61-desktop-connection-disconnected-fault-text.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan61DesktopConnectionDisconnectedFaultTextW7388", testing, StringComparison.Ordinal);

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
