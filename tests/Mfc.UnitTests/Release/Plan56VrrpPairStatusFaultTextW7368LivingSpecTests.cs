using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-368: PLAN-56 inventory documents sole DESK-VRRP-FAULT-01 rank
/// (2 static VRRP pair status sentences drop the correlation id already on ErrorText)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan56VrrpPairStatusFaultTextW7368LivingSpecTests
{
    [Fact]
    public void Ac1Plan56InventoryDocumentsSoleDeskVrrpFault01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan56 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-56-vrrp-pair-status-fault-text.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("PLAN-56 — VRRP pair status fault text after capture progress fault correlation", plan56, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan56, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", plan56, StringComparison.Ordinal);
        Assert.Contains("dcde8dca", plan56, StringComparison.Ordinal);
        Assert.Contains("f5560b4c", plan56, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan56, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VRRP pair consistency failed", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-370", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-371", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-369", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-368", plan56, StringComparison.Ordinal);
        Assert.Contains("DesktopRpcFaultText.Format", plan56, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan56, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-368 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-370", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-369", limitations, StringComparison.Ordinal);
        Assert.Contains("dcde8dca", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-368 | [#1143](https://github.com/sesquicadaver/MTDirector/issues/1143) | PLAN-56 — Inventory VRRP pair status fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-369 | [#1144](https://github.com/sesquicadaver/MTDirector/issues/1144) | Seed first PLAN-56 atomic row after inventory → DESK-VRRP-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-370 | [#1146](https://github.com/sesquicadaver/MTDirector/issues/1146) | DESK-VRRP-FAULT-01 — Show RPC fault text on VRRP pair status | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-371 | [#1147](https://github.com/sesquicadaver/MTDirector/issues/1147) | Seed next after DESK-VRRP-FAULT-01 (PLAN-56 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-369", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-370", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-56", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-56-vrrp-pair-status-fault-text.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-56-vrrp-pair-status-fault-text.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan56VrrpPairStatusFaultTextW7368", testing, StringComparison.Ordinal);

        Assert.Equal(1, Count(node, "VrrpPairStatusText = \"VRRP pair consistency failed.\""));
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex)", node, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\"", node, StringComparison.Ordinal);
        Assert.DoesNotContain("correlation", node, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public static string Format(RpcException exception)", fault, StringComparison.Ordinal);
        Assert.Equal(4, Count(connection, "DesktopRpcFaultText.Format(ex)"));
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
