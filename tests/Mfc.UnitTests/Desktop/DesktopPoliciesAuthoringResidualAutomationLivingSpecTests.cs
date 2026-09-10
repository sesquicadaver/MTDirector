using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-POLICY-EDIT-01 / W7-185: Policies authoring residual buttons expose AutomationProperties.Name.</summary>
public sealed class DesktopPoliciesAuthoringResidualAutomationLivingSpecTests
{
    [Fact]
    public void Ac1PoliciesAuthoringResidualButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        AssertNameBeforeCommand(main, "Policies.LoadCommand", "Load");
        AssertNameBeforeCommand(main, "Policies.CreateDraftCommand", "Create draft");
        AssertNameBeforeCommand(main, "Policies.RefreshSafetyAnalysisCommand", "Analyze safety");
        AssertNameBeforeCommand(main, "Policies.AddRuleCommand", "Add rule");
        AssertNameBeforeCommand(main, "Policies.UpdateRuleCommand", "Update rule");
        AssertNameBeforeCommand(main, "Policies.DeleteRuleCommand", "Delete rule");
        AssertNameBeforeCommand(main, "Policies.MoveRuleUpCommand", "Move up");
        AssertNameBeforeCommand(main, "Policies.MoveRuleDownCommand", "Move down");
        AssertNameBeforeCommand(main, "Policies.ComposeCommand", "Compose findings");
        AssertNameBeforeCommand(main, "Policies.DiffCommand", "Semantic diff");
        AssertNameBeforeCommand(main, "Policies.CompileCommand", "Compile artifacts");
    }

    [Fact]
    public void Ac2Plan21AndTestingDocLockA11yPolicyEdit01()
    {
        string root = FindRepoRoot();
        string plan21 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-21-desktop-policies-authoring-residual-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-POLICY-EDIT-01", plan21, StringComparison.Ordinal);
        Assert.Contains("W7-185 DONE", plan21, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesAuthoringResidualAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-185 Living Spec lock)", limitations, StringComparison.Ordinal);
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
