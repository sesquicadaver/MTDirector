using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-ONBOARD-01 / W7-83: Desktop Onboarding Living Spec is present and documented.</summary>
public sealed class CtDeskOnboard01DesktopOnboardingLivingSpecTests
{
    [Fact]
    public void Ac1DesktopOnboardingLivingSpecAndPlan07MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopOnboardingLivingSpecTests.cs");
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopOnboardingLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3ValidateLoadsFindingsWhenNodeSelected", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ViewModelExposesValidatePlanStartRollbackRecoveryWithoutScriptOrArbitraryWrite", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ValidateRequiresInventoryNodeSelection", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-83 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-ONBOARD-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopOnboardingLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-83 Living Spec lock)", limitations, StringComparison.Ordinal);
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
