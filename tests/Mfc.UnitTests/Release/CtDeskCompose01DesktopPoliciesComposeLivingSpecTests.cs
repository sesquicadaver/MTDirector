using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-COMPOSE-01 / W7-115: Desktop Policies Compose+RecordAnalysis Living Spec is present and documented.</summary>
public sealed class CtDeskCompose01DesktopPoliciesComposeLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesComposeLivingSpecAndPlan11MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesComposeLivingSpecTests.cs");
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesComposeLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesComposeAndRecordAnalysisCommandsAndSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac3ComposeCommandParsesNodeIdAndCallsPanelInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4RecordAnalysisUsesLogicalHashAndPanelRecordAnalysisRunInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac5PanelComposeAndRecordAnalysisDelegateWithoutLocalSemanticEngine", body, StringComparison.Ordinal);
        Assert.Contains("W7-115 DONE", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-COMPOSE-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesComposeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-115 Living Spec lock)", limitations, StringComparison.Ordinal);
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
