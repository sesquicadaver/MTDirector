using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-194: PLAN-23 inventory documents ranked DESK-A11Y-POLICY-OBJ-* rows and seeds DESK-A11Y-POLICY-OBJ-01.</summary>
public sealed class Plan23DesktopPoliciesCatalogObjectW7194LivingSpecTests
{
    [Fact]
    public void Ac1Plan23InventoryDocumentsRankedRowsAndSeedsDeskA11yPolicyObj01()
    {
        string root = RepoRoot();
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan22 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-22-desktop-policies-ack-record-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-23 — Desktop Policies catalog/object AutomationProperties Living Spec product tranche", plan23, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-01", plan23, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-02", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-195", plan23, StringComparison.Ordinal);
        Assert.Contains("Refresh catalog", plan23, StringComparison.Ordinal);
        Assert.Contains("Upsert address", plan23, StringComparison.Ordinal);
        Assert.Contains("Upsert service", plan23, StringComparison.Ordinal);
        Assert.Contains("Replace contracts", plan23, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan23, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan23, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-194 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-23", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-195", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-23", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-23-desktop-policies-catalog-object-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-23-desktop-policies-catalog-object-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan22, StringComparison.Ordinal);
        Assert.Contains("PLAN-23", plan22, StringComparison.Ordinal);
        Assert.Contains("Plan23DesktopPoliciesCatalogObjectW7194", testing, StringComparison.Ordinal);
        Assert.Contains("Policies.RefreshCatalogCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.UpsertAddressCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.UpsertServiceCommand", main, StringComparison.Ordinal);
        Assert.Contains("Policies.ReplaceContractsCommand", main, StringComparison.Ordinal);
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
