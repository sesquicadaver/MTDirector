using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-204: PLAN-25 inventory documents ranked DESK-A11Y-INV-* rows and seeds DESK-A11Y-INV-01.</summary>
public sealed class Plan25DesktopInventoryZonesAddRouterW7204LivingSpecTests
{
    [Fact]
    public void Ac1Plan25InventoryDocumentsRankedRowsAndSeedsDeskA11yInv01()
    {
        string root = RepoRoot();
        string plan25 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-25-desktop-inventory-zones-add-router-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-25 — Desktop Inventory / Zones / Add-router AutomationProperties Living Spec product tranche", plan25, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan25, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-01", plan25, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-02", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-207", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-208", plan25, StringComparison.Ordinal);
        Assert.Contains("Resolve node", plan25, StringComparison.Ordinal);
        Assert.Contains("Update zone", plan25, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan25, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-204 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-01", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-207", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-207", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-25", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-25-desktop-inventory-zones-add-router-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-25-desktop-inventory-zones-add-router-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan25DesktopInventoryZonesAddRouterW7204", testing, StringComparison.Ordinal);
        Assert.Contains("Create / register", main, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Create / register\"", main, StringComparison.Ordinal);
        Assert.Contains("UpsertBindingCommand", main, StringComparison.Ordinal);
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
