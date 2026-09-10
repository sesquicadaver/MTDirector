using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-POLICY-02 / W7-182: Policies lifecycle + Connect/Disconnect + Incident Names; PLAN-20 COMPLETE.</summary>
public sealed class DesktopPoliciesLifecycleActionA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1PoliciesLifecycleAndShellIncidentActionNamesMatrixLocked()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);

        AssertNameBeforeCommand(main, "Policies.ValidateCommand", "Validate");
        AssertNameBeforeCommand(main, "Policies.SubmitCommand", "Submit for review");
        AssertNameBeforeCommand(main, "Policies.ApproveCommand", "Approve");
        AssertNameBeforeCommand(main, "Policies.BindCommand", "Bind");
        AssertNameBeforeCommand(main, "Policies.DeployCommand", "Deploy");

        AssertNameBeforeCommand(main, "ConnectCommand", "Connect");
        AssertNameBeforeCommand(main, "DisconnectCommand", "Disconnect");

        int ingest = main.IndexOf("Incident.IngestCommand", StringComparison.Ordinal);
        Assert.True(ingest > 0);
        string ingestBefore = main.Substring(Math.Max(0, ingest - 280), Math.Min(280, ingest));
        Assert.Contains("AutomationProperties.Name=\"Ingest signal\"", ingestBefore, StringComparison.Ordinal);

        int bind = main.IndexOf("Incident.BindAssessmentCommand", StringComparison.Ordinal);
        Assert.True(bind > 0);
        string bindBefore = main.Substring(Math.Max(0, bind - 280), Math.Min(280, bind));
        Assert.Contains("AutomationProperties.Name=\"Bind assessment\"", bindBefore, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan20CompleteAndDocsLockA11yPolicy02()
    {
        string root = FindRepoRoot();
        string plan20 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-20-desktop-policies-lifecycle-action-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-20 COMPLETE", plan20, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-02", plan20, StringComparison.Ordinal);
        Assert.Contains("W7-182 DONE", plan20, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesLifecycleActionA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-182 Living Spec lock)", limitations, StringComparison.Ordinal);
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
