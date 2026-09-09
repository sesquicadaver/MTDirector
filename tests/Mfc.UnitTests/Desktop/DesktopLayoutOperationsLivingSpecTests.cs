using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-07 / W7-141: Operations Onboarding/Deploy MaxHeight → layout token floors Living Spec.</summary>
public sealed class DesktopLayoutOperationsLivingSpecTests
{
    [Fact]
    public void Ac1OnboardingAndDeployListsUseListMinHeightAndNoMaxHeightCaps()
    {
        string slice = OperationsSlice();
        Assert.Contains("Onboarding.Findings", slice, StringComparison.Ordinal);
        Assert.Contains("Deployment.SemanticDiffRows", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"100\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"120\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"140\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2OnboardingAndDeployPrimaryListsAllBindListMinHeight()
    {
        string slice = OperationsSlice();
        string[] bindings =
        [
            "Onboarding.Findings",
            "Onboarding.Placements",
            "Onboarding.ProgressLines",
            "Deployment.SemanticDiffRows",
            "Deployment.SemanticDiffLines",
            "Deployment.ArtifactLines",
            "Deployment.OrderLines",
            "Deployment.ProbeAndWatchdogLines",
            "Deployment.ProgressLines",
        ];

        foreach (string binding in bindings)
        {
            int idx = slice.IndexOf($"ItemsSource=\"{{Binding {binding}}}\"", StringComparison.Ordinal);
            Assert.True(idx >= 0, $"Missing ItemsSource binding {binding}");
            int listStart = slice.LastIndexOf("<ListBox", idx, StringComparison.Ordinal);
            Assert.True(listStart >= 0, $"ListBox open missing for {binding}");
            int listTagEnd = slice.IndexOf('>', listStart);
            string openTag = slice[listStart..listTagEnd];
            Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", openTag, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout07()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-07", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-141 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-07", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Onboarding", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutOperationsLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string OperationsSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- Operations: Onboarding + Deploy + recovery -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "Operations marker missing.");
        int end = main.IndexOf("<!-- Drift (no automatic fix) -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Drift marker missing after Operations.");
        return main[start..end];
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
