using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-199: PLAN-24 inventory documents ranked DESK-A11Y-OPS-* rows and seeds DESK-A11Y-OPS-01.</summary>
public sealed class Plan24DesktopOnboardingDeploymentW7199LivingSpecTests
{
    [Fact]
    public void Ac1Plan24InventoryDocumentsRankedRowsAndSeedsDeskA11yOps01()
    {
        string root = RepoRoot();
        string plan24 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-24-desktop-onboarding-deployment-automation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string main = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("PLAN-24 — Desktop Onboarding/Deployment AutomationProperties Living Spec product tranche", plan24, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-01", plan24, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-02", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-200", plan24, StringComparison.Ordinal);
        Assert.Contains("Validate prerequisites", plan24, StringComparison.Ordinal);
        Assert.Contains("Create plan", plan24, StringComparison.Ordinal);
        Assert.Contains("Recovery status", plan24, StringComparison.Ordinal);
        Assert.Contains("MainWindow.axaml", plan24, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan24, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-199 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-24", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-OPS-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-200", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-24", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-24-desktop-onboarding-deployment-automation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-24-desktop-onboarding-deployment-automation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan23, StringComparison.Ordinal);
        Assert.Contains("PLAN-24", plan23, StringComparison.Ordinal);
        Assert.Contains("Plan24DesktopOnboardingDeploymentW7199", testing, StringComparison.Ordinal);
        Assert.Contains("Onboarding.ValidateCommand", main, StringComparison.Ordinal);
        Assert.Contains("Onboarding.CreatePlanCommand", main, StringComparison.Ordinal);
        Assert.Contains("Deployment.CreatePlanCommand", main, StringComparison.Ordinal);
        Assert.Contains("Deployment.RecoveryCommand", main, StringComparison.Ordinal);
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
