using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-369: known-limitations / queue seed locked DESK-VRRP-FAULT-01 (W7-370) after PLAN-56 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7369LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskVrrpFault01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan56 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-56-vrrp-pair-status-fault-text.md"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-369 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-370", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-371", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-370**", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-374 (#1154)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-369", plan, StringComparison.Ordinal);
        Assert.Contains("W7-370", plan, StringComparison.Ordinal);
        Assert.Contains("W7-371", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-374 (#1154)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-369 (#1144) DONE", plan56, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-370", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-371", plan56, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-374 (#1154)", plan56, StringComparison.Ordinal);

        Assert.Equal(1, Count(node, "VrrpPairStatusText = \"VRRP pair consistency failed.\""));
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("ErrorText = DesktopRpcFaultText.Format(ex)", node, StringComparison.Ordinal);
        Assert.DoesNotContain("correlation", node, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public static string Format(RpcException exception)", fault, StringComparison.Ordinal);
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
