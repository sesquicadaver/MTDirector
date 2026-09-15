using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-SNAP-01 / W7-240: Snapshot / Semantic-diff primary actions expose AutomationProperties.Name.</summary>
public sealed class DesktopSnapshotAutomationLivingSpecTests
{
    [Fact]
    public void Ac1SnapshotAndSemanticDiffButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        AssertNameBeforeCommand(main, "Snapshot.ReloadCommand", "Reload");
        AssertNameBeforeCommand(main, "Snapshot.CaptureCommand", "Capture");
        AssertNameBeforeCommand(main, "Snapshot.CopySanitizedCommand", "Copy sanitized");
        AssertNameBeforeCommand(main, "Diff.CompareCommand", "Compare");
        AssertNameBeforeCommand(main, "Diff.ReloadCapturesCommand", "Reload captures");
    }

    [Fact]
    public void Ac2Plan27AndTestingDocLockA11ySnap01()
    {
        string root = FindRepoRoot();
        string plan27 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-27-desktop-snapshot-panel-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-SNAP-01", plan27, StringComparison.Ordinal);
        Assert.Contains("W7-240 DONE", plan27, StringComparison.Ordinal);
        Assert.Contains("DesktopSnapshotAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-240 Living Spec lock)", limitations, StringComparison.Ordinal);
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
