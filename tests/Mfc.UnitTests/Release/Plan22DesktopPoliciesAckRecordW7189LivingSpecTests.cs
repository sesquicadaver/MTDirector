using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-189: PLAN-22 inventory documents ranked DESK-A11Y-POLICY-ACK-* rows and seeds DESK-A11Y-POLICY-ACK-01.</summary>
public sealed class Plan22DesktopPoliciesAckRecordW7189LivingSpecTests
{
    [Fact]
    public void Ac1Plan22InventoryDocumentsRankedRowsAndSeedsDeskA11yPolicyAck01()
    {
        string root = RepoRoot();
        string plan22 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-22-desktop-policies-ack-record-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan21 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-21-desktop-policies-authoring-residual-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-22 — Desktop Policies acknowledge/record-analysis AutomationProperties Living Spec product tranche", plan22, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-01", plan22, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-02", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-190", plan22, StringComparison.Ordinal);
        Assert.Contains("Record analysis", plan22, StringComparison.Ordinal);
        Assert.Contains("Acknowledge warning", plan22, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan22, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan22, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-189 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-22", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-190", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-22", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-22-desktop-policies-ack-record-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-22-desktop-policies-ack-record-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan21, StringComparison.Ordinal);
        Assert.Contains("PLAN-22", plan21, StringComparison.Ordinal);
        Assert.Contains("Plan22DesktopPoliciesAckRecordW7189", testing, StringComparison.Ordinal);
        Assert.Contains("Policies.RecordAnalysisCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.AcknowledgeWarningCommand", main, StringComparison.Ordinal);
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
