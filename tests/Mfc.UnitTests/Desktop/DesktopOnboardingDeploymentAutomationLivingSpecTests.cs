using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-OPS-01 / W7-200: Onboarding/Deployment primary actions expose AutomationProperties.Name.</summary>
public sealed class DesktopOnboardingDeploymentAutomationLivingSpecTests
{
    [Fact]
    public void Ac1OnboardingAndDeploymentButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        AssertNameBeforeCommand(main, "Onboarding.ValidateCommand", "Validate prerequisites");
        AssertNameBeforeCommand(main, "Onboarding.CreatePlanCommand", "Create plan");
        AssertNameBeforeCommand(main, "Onboarding.StartCommand", "Start");
        AssertNameBeforeCommand(main, "Onboarding.RollbackCommand", "Rollback");
        AssertNameBeforeCommand(main, "Onboarding.RecoveryCommand", "Recovery status");

        AssertNameBeforeCommand(main, "Deployment.CreatePlanCommand", "Create plan");
        AssertNameBeforeCommand(main, "Deployment.StartCommand", "Start");
        AssertNameBeforeCommand(main, "Deployment.RollbackCommand", "Rollback");
        AssertNameBeforeCommand(main, "Deployment.RecoveryCommand", "Recovery status");
    }

    [Fact]
    public void Ac2Plan24AndTestingDocLockA11yOps01()
    {
        string root = FindRepoRoot();
        string plan24 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-24-desktop-onboarding-deployment-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-OPS-01", plan24, StringComparison.Ordinal);
        Assert.Contains("W7-200 DONE", plan24, StringComparison.Ordinal);
        Assert.Contains("DesktopOnboardingDeploymentAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-200 Living Spec lock)", limitations, StringComparison.Ordinal);
    }

    private static void AssertNameBeforeCommand(string main, string commandBinding, string name)
    {
        string needle = $"Command=\"{{Binding {commandBinding}}}\"";
        int index = main.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(index > 0, $"Missing {needle}");
        string before = main.Substring(Math.Max(0, index - 280), Math.Min(280, index));
        Assert.Contains($"AutomationProperties.Name=\"{name}\"", before, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
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
