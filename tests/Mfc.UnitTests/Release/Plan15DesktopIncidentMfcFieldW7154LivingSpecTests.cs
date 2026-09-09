using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-154: PLAN-15 inventory documents ranked DESK-FIELD-* rows and seeds DESK-FIELD-01.</summary>
public sealed class Plan15DesktopIncidentMfcFieldW7154LivingSpecTests
{
    [Fact]
    public void Ac1Plan15InventoryDocumentsRankedRowsAndSeedsDeskField01()
    {
        string root = RepoRoot();
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-15 — Desktop Incident mfc-field style hygiene Living Spec product tranche", plan15, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-01", plan15, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-02", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-155", plan15, StringComparison.Ordinal);
        Assert.Contains("mfc-field", plan15, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan15, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-154 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-15", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-155", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-15", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-15-desktop-incident-mfc-field-style-hygiene.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-15-desktop-incident-mfc-field-style-hygiene.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan14, StringComparison.Ordinal);
        Assert.Contains("PLAN-15", plan14, StringComparison.Ordinal);
        Assert.Contains("Plan15DesktopIncidentMfcFieldW7154", testing, StringComparison.Ordinal);
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
