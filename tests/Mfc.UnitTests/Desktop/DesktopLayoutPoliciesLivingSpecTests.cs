using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-05 / W7-137: Policies MaxHeight cascade → layout token floors Living Spec.</summary>
public sealed class DesktopLayoutPoliciesLivingSpecTests
{
    [Fact]
    public void Ac1PoliciesListsUseListMinHeightAndNoTinyMaxHeightCaps()
    {
        string slice = PoliciesSlice();
        Assert.Contains("Policies.Catalog", slice, StringComparison.Ordinal);
        Assert.Contains("Policies.Rules", slice, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"80\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"100\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"120\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"140\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"160\"", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"180\"", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2PoliciesPrimaryListsAllBindListMinHeight()
    {
        string slice = PoliciesSlice();
        string[] bindings =
        [
            "Policies.Catalog",
            "Policies.ManagementPathFindingLines",
            "Policies.FastTrackFindingLines",
            "Policies.SafetyWitnessLines",
            "Policies.SafetySystemTestLines",
            "Policies.Rules",
            "Policies.AddressObjects",
            "Policies.ServiceObjects",
            "Policies.ChainContracts",
            "Policies.DiffRows",
            "Policies.DiffLines",
            "Policies.Findings",
            "Policies.CompileArtifactLines",
        ];

        foreach (string binding in bindings)
        {
            int idx = slice.IndexOf(binding, StringComparison.Ordinal);
            Assert.True(idx >= 0, $"Missing binding {binding}");
            // Look at surrounding ListBox open tag (search backwards for <ListBox)
            int listStart = slice.LastIndexOf("<ListBox", idx, StringComparison.Ordinal);
            Assert.True(listStart >= 0, $"ListBox open missing for {binding}");
            int listTagEnd = slice.IndexOf('>', listStart);
            string openTag = slice[listStart..listTagEnd];
            Assert.Contains("MinHeight=\"{StaticResource Mfc.ListMinHeight}\"", openTag, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac3Plan13AndDesktopLayoutDocLockLayout05()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-05", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-137 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-05", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("Policies", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutPoliciesLivingSpecTests", testing, StringComparison.Ordinal);
    }

    private static string PoliciesSlice()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        int start = main.IndexOf("<!-- Policies -->", StringComparison.Ordinal);
        Assert.True(start >= 0, "Policies marker missing.");
        int end = main.IndexOf("<!-- Operations: Onboarding + Deploy + recovery -->", start, StringComparison.Ordinal);
        Assert.True(end > start, "Operations marker missing after Policies.");
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
