using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-POLICY-02 / W7-96: Desktop Policy safety Living Spec is present and documented.</summary>
public sealed class CtDeskPolicy02DesktopPolicySafetyLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPolicySafetyLivingSpecAndPlan08MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPolicySafetyLivingSpecTests.cs");
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPolicySafetyLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3RefreshSafetyAnalysisBindsHashesFlagsFindingsAndWitnesses", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ViewModelExposesRefreshSafetyAnalysisCommandAndResultSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac4SafetyAnalysisRequiresDevicePrefixesAndConnectedController", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsSafetyAnalysisInputsAndResults", body, StringComparison.Ordinal);
        Assert.Contains("W7-96 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("PLAN-08", plan08, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-POLICY-02", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPolicySafetyLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-96 Living Spec lock)", limitations, StringComparison.Ordinal);
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
