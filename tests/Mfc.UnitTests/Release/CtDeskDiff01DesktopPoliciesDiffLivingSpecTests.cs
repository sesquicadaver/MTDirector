using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-DIFF-01 / W7-108: Desktop Policies Diff Living Spec is present and documented.</summary>
public sealed class CtDeskDiff01DesktopPoliciesDiffLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesDiffLivingSpecAndPlan10MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesDiffLivingSpecTests.cs");
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesDiffLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesDiffCommandBaselineAndTypedDiffSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac3DiffCommandGuardsOnCanOperateAndParsesBaselineAfterRevisionIdsInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4PanelDiffAsyncMapsSemanticPacketRiskRuleFindingKindsWithoutLocalEngine", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsPoliciesDiffBaselineCommandRowsAndLines", body, StringComparison.Ordinal);
        Assert.Contains("W7-108 DONE", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-DIFF-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesDiffLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-108 Living Spec lock)", limitations, StringComparison.Ordinal);
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
