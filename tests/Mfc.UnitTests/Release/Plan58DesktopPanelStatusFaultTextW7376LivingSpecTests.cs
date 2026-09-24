using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-376: PLAN-58 inventory documents sole DESK-PANEL-FAULT-01 rank
/// (seven RpcException catches still write a static panel status while ErrorText already has DesktopRpcFaultText)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan58DesktopPanelStatusFaultTextW7376LivingSpecTests
{
    [Fact]
    public void Ac1Plan58InventoryDocumentsSoleDeskPanelFault01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan58 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-58-desktop-panel-status-fault-text.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string audit = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/AuditViewModel.cs"));
        string incident = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/IncidentViewModel.cs"));
        string routing = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/RoutingAssuranceViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("PLAN-58 — Desktop panel status fault text after VRRP capture-progress fault text", plan58, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan58, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", plan58, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan58, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("55765d52", plan58, StringComparison.Ordinal);
        Assert.Contains("c665a34c", plan58, StringComparison.Ordinal);
        Assert.Contains("Drift load failed", plan58, StringComparison.Ordinal);
        Assert.Contains("GetDriftEvent failed", plan58, StringComparison.Ordinal);
        Assert.Contains("Audit load failed", plan58, StringComparison.Ordinal);
        Assert.Contains("Incident ingest failed", plan58, StringComparison.Ordinal);
        Assert.Contains("Incident assessment bind failed", plan58, StringComparison.Ordinal);
        Assert.Contains("Routing assurance load failed", plan58, StringComparison.Ordinal);
        Assert.Contains("GetNodeWorkflow failed", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-376", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-377", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-378", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-379", plan58, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", plan58, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-376 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-378", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-377", limitations, StringComparison.Ordinal);
        Assert.Contains("c665a34c", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-376 | [#1159](https://github.com/sesquicadaver/MTDirector/issues/1159) | PLAN-58 — Inventory Desktop panel status fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-377 | [#1160](https://github.com/sesquicadaver/MTDirector/issues/1160) | Seed first PLAN-58 atomic row after inventory → DESK-PANEL-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-378 | [#1162](https://github.com/sesquicadaver/MTDirector/issues/1162) | DESK-PANEL-FAULT-01 — Show RPC fault text on panel status lines | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-379 | [#1163](https://github.com/sesquicadaver/MTDirector/issues/1163) | Seed next after DESK-PANEL-FAULT-01 (PLAN-58 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-377", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-378", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-58", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-58-desktop-panel-status-fault-text.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-58-desktop-panel-status-fault-text.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan58DesktopPanelStatusFaultTextW7376", testing, StringComparison.Ordinal);

        Assert.Equal(1, Count(drift, "StatusText = \"Drift load failed.\""));
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Equal(1, Count(drift, "StatusText = \"GetDriftEvent failed; showing list payload.\""));
        Assert.Contains("StatusText = $\"GetDriftEvent failed; showing list payload. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Equal(1, Count(audit, "StatusText = \"Audit load failed.\""));
        Assert.Contains("StatusText = $\"Audit load failed. {fault}\"", audit, StringComparison.Ordinal);
        Assert.Equal(1, Count(incident, "StatusText = \"Incident ingest failed.\""));
        Assert.Contains("StatusText = $\"Incident ingest failed. {fault}\"", incident, StringComparison.Ordinal);
        Assert.Equal(1, Count(incident, "StatusText = \"Incident assessment bind failed.\""));
        Assert.Contains("StatusText = $\"Incident assessment bind failed. {fault}\"", incident, StringComparison.Ordinal);
        Assert.Equal(1, Count(routing, "StatusText = \"Routing assurance load failed.\""));
        Assert.Contains("StatusText = $\"Routing assurance load failed. {fault}\"", routing, StringComparison.Ordinal);
        Assert.Equal(1, Count(node, "DeploymentReadinessText = \"GetNodeWorkflow failed.\""));
        Assert.Contains("DeploymentReadinessText = $\"GetNodeWorkflow failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Equal(
            8,
            Count(drift, "string fault = DesktopRpcFaultText.Format(ex);")
            + Count(audit, "string fault = DesktopRpcFaultText.Format(ex);")
            + Count(incident, "string fault = DesktopRpcFaultText.Format(ex);")
            + Count(routing, "string fault = DesktopRpcFaultText.Format(ex);")
            + Count(node, "string fault = DesktopRpcFaultText.Format(ex);"));
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
