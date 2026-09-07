using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-DEPLOY-01 / W7-81: Desktop Deployment Living Spec is present and documented.</summary>
public sealed class CtDeskDeploy01DesktopDeploymentLivingSpecTests
{
    [Fact]
    public void Ac1DesktopDeploymentLivingSpecAndPlan07MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopDeploymentLivingSpecTests.cs");
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopDeploymentLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3CreatePlanLoadsSemanticDiffWhenNodeSelected", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ViewModelExposesPlanStartRollbackRecoveryWithoutForceApplyOrRawCommands", body, StringComparison.Ordinal);
        Assert.Contains("Ac4CreatePlanRequiresInventoryNodeSelection", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-81 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-DEPLOY-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopDeploymentLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-81 Living Spec lock)", limitations, StringComparison.Ordinal);
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
