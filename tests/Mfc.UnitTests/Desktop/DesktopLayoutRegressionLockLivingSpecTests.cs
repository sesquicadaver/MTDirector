using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-LAYOUT-10 / W7-147: PLAN-13 regression lock — all DESK-LAYOUT-00…09 Living Specs present.</summary>
public sealed class DesktopLayoutRegressionLockLivingSpecTests
{
    private static readonly (string RelativePath, string AcMarker)[] RequiredSpecs =
    [
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutTokensLivingSpecTests.cs", "Ac1AppAxamlDefinesSharedLayoutHeightTokens"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutSnapshotLivingSpecTests.cs", "Ac1SnapshotPanelUsesSinglePrimaryStarAndDetailStarRows"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutSemanticDiffLivingSpecTests.cs", "Ac1SemanticDiffUsesEntryStarSplitterAndDetailStar"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutDriftLivingSpecTests.cs", "Ac1DriftUsesThreeStarPanesWithTwoRowSplitters"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutAuditLivingSpecTests.cs", "Ac1AuditUsesListStarSplitterAndPayloadStar"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutPoliciesLivingSpecTests.cs", "Ac1PoliciesListsUseListMinHeightAndNoTinyMaxHeightCaps"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutNodeRoutingLivingSpecTests.cs", "Ac1NodeAndRoutingListsUseListMinHeightAndNoMaxHeightCaps"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutOperationsLivingSpecTests.cs", "Ac1OnboardingAndDeployListsUseListMinHeightAndNoMaxHeightCaps"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutShellChromeLivingSpecTests.cs", "Ac1ShellChromeUsesAutoSplitterColumnsBetweenPanes"),
        ("tests/Mfc.UnitTests/Desktop/DesktopLayoutZonesLivingSpecTests.cs", "Ac1ZonesListsUseListMinHeightAndNoMaxHeightCaps"),
    ];

    [Fact]
    public void Ac1AllDeskLayout00Through09LivingSpecsExistWithPrimaryAc()
    {
        string root = FindRepoRoot();
        foreach ((string relativePath, string acMarker) in RequiredSpecs)
        {
            string path = Path.Combine(root, relativePath);
            Assert.True(File.Exists(path), $"{relativePath} missing.");
            string body = File.ReadAllText(path);
            Assert.Contains(acMarker, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2MainWindowHasNoPrimaryListMaxHeightCascadeResidue()
    {
        string main = File.ReadAllText(Path.Combine(FindRepoRoot(), "src/Mfc.Desktop/MainWindow.axaml"));
        // PLAN-13 closed the operator-reported MaxHeight cascades on primary lists.
        Assert.DoesNotContain("MaxHeight=\"80\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"100\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"120\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"140\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"160\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"180\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"200\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"220\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"240\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"280\"", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan13CompleteAndDesktopLayoutDocLockLayout10()
    {
        string root = FindRepoRoot();
        string plan13 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-13-desktop-layout-density.md"));
        string layoutDoc = File.ReadAllText(Path.Combine(root, "docs/development/desktop-layout.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("DESK-LAYOUT-10", plan13, StringComparison.Ordinal);
        Assert.Contains("W7-147 DONE", plan13, StringComparison.Ordinal);
        Assert.Contains("PLAN-13 COMPLETE", plan13, StringComparison.Ordinal);
        Assert.Contains("DESK-LAYOUT-10", layoutDoc, StringComparison.Ordinal);
        Assert.Contains("DesktopLayoutRegressionLockLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("PLAN-13 COMPLETE", layoutDoc, StringComparison.Ordinal);
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
