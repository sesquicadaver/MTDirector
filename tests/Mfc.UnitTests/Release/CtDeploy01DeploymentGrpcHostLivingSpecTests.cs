using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT-DEPLOY-01 / W7-59: DeploymentService GrpcHost contract is present and documented.</summary>
public sealed class CtDeploy01DeploymentGrpcHostLivingSpecTests
{
    [Fact]
    public void Ac1DeploymentGrpcHostTestsAndPlan04MatrixExist()
    {
        string root = RepoRoot();
        string hostTests = Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/DeploymentGrpcHostTests.cs");
        string scripted = Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/ScriptedDeploymentRuntime.cs");
        string plan04 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-04-contract-tests.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(hostTests), "DeploymentGrpcHostTests.cs missing.");
        Assert.True(File.Exists(scripted), "ScriptedDeploymentRuntime.cs missing.");
        Assert.Contains("CreatePlanStartWatchAndRecoveryStatus", File.ReadAllText(hostTests), StringComparison.Ordinal);
        Assert.Contains("CreatePlanAndRollbackAreIdempotent", File.ReadAllText(hostTests), StringComparison.Ordinal);
        Assert.Contains("W7-59 DONE", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-DEPLOY-01", testing, StringComparison.Ordinal);
        Assert.Contains("DeploymentGrpcHostTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-59 Living Spec lock)", limitations, StringComparison.Ordinal);
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
