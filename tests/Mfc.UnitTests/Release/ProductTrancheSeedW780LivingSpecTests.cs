using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-80: known-limitations / queue seed locks next PLAN-07 row (W7-81 DESK-DEPLOY-01).</summary>
public sealed class ProductTrancheSeedW780LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskDeploy01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-80 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-DEPLOY-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-81", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-81", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-81", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-DEPLOY-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-79 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-DEPLOY-01", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-81", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-80", roadmap, StringComparison.Ordinal);
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
