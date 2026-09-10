using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-POLICY-ACK-01 / W7-190: Policies Record analysis / Acknowledge warning expose AutomationProperties.Name.</summary>
public sealed class DesktopPoliciesAckRecordAutomationLivingSpecTests
{
    [Fact]
    public void Ac1PoliciesAckRecordButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        AssertNameBeforeCommand(main, "Policies.RecordAnalysisCommand", "Record analysis");
        AssertNameBeforeCommand(main, "Policies.AcknowledgeWarningCommand", "Acknowledge warning");
    }

    [Fact]
    public void Ac2Plan22AndTestingDocLockA11yPolicyAck01()
    {
        string root = FindRepoRoot();
        string plan22 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-22-desktop-policies-ack-record-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-POLICY-ACK-01", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-190 DONE", plan22, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesAckRecordAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-190 Living Spec lock)", limitations, StringComparison.Ordinal);
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
