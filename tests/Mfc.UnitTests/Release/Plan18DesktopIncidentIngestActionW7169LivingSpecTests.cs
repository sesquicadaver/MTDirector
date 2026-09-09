using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-169: PLAN-18 inventory documents ranked DESK-A11Y-INGEST-* rows and seeds DESK-A11Y-INGEST-01.</summary>
public sealed class Plan18DesktopIncidentIngestActionW7169LivingSpecTests
{
    [Fact]
    public void Ac1Plan18InventoryDocumentsRankedRowsAndSeedsDeskA11yIngest01()
    {
        string root = RepoRoot();
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan17 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-17-desktop-incident-bind-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-18 — Desktop Incident ingest-action AutomationProperties Living Spec product tranche", plan18, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-01", plan18, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-02", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-170", plan18, StringComparison.Ordinal);
        Assert.Contains("Ingest signal", plan18, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan18, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-169 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-18", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-170", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-18", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-18-desktop-incident-ingest-action-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-18-desktop-incident-ingest-action-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan17, StringComparison.Ordinal);
        Assert.Contains("PLAN-18", plan17, StringComparison.Ordinal);
        Assert.Contains("Plan18DesktopIncidentIngestActionW7169", testing, StringComparison.Ordinal);
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
