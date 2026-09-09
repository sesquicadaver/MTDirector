using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-164: PLAN-17 inventory documents ranked DESK-A11Y-ACTION-* rows and seeds DESK-A11Y-ACTION-01.</summary>
public sealed class Plan17DesktopIncidentBindActionW7164LivingSpecTests
{
    [Fact]
    public void Ac1Plan17InventoryDocumentsRankedRowsAndSeedsDeskA11yAction01()
    {
        string root = RepoRoot();
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-17 — Desktop Incident bind-action AutomationProperties Living Spec product tranche", plan17, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-01", plan17, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-02", plan17, StringComparison.Ordinal);
        Assert.Contains("W7-165", plan17, StringComparison.Ordinal);
        Assert.Contains("Bind assessment", plan17, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan17, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-164 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-17", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-ACTION-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-165", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-17", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-17-desktop-incident-bind-action-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-17-desktop-incident-bind-action-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan16, StringComparison.Ordinal);
        Assert.Contains("PLAN-17", plan16, StringComparison.Ordinal);
        Assert.Contains("Plan17DesktopIncidentBindActionW7164", testing, StringComparison.Ordinal);
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
