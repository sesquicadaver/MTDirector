using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-POLICY-01 / W7-180: Policies lifecycle buttons expose AutomationProperties.Name.</summary>
public sealed class DesktopPoliciesLifecycleActionAutomationLivingSpecTests
{
    [Fact]
    public void Ac1PoliciesLifecycleButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        AssertNameBeforeCommand(main, "Policies.ValidateCommand", "Validate");
        AssertNameBeforeCommand(main, "Policies.SubmitCommand", "Submit for review");
        AssertNameBeforeCommand(main, "Policies.ApproveCommand", "Approve");
        AssertNameBeforeCommand(main, "Policies.BindCommand", "Bind");
        AssertNameBeforeCommand(main, "Policies.DeployCommand", "Deploy");
    }

    [Fact]
    public void Ac2Plan20AndTestingDocLockA11yPolicy01()
    {
        string root = FindRepoRoot();
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-POLICY-01", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-180 DONE", plan20, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesLifecycleActionAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-180 Living Spec lock)", limitations, StringComparison.Ordinal);
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
