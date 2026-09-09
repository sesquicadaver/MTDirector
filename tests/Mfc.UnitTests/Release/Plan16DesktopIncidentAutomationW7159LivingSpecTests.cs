using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-159: PLAN-16 inventory documents ranked DESK-A11Y-* rows and seeds DESK-A11Y-01.</summary>
public sealed class Plan16DesktopIncidentAutomationW7159LivingSpecTests
{
    [Fact]
    public void Ac1Plan16InventoryDocumentsRankedRowsAndSeedsDeskA11y01()
    {
        string root = RepoRoot();
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-16 — Desktop Incident AutomationProperties accessible-name Living Spec product tranche", plan16, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-01", plan16, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-02", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-160", plan16, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name", plan16, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan16, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-159 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-16", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-160", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-16", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-16-desktop-incident-automation-properties.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-16-desktop-incident-automation-properties.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan15, StringComparison.Ordinal);
        Assert.Contains("PLAN-16", plan15, StringComparison.Ordinal);
        Assert.Contains("Plan16DesktopIncidentAutomationW7159", testing, StringComparison.Ordinal);
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
