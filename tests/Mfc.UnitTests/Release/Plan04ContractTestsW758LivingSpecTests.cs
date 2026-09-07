using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-58: PLAN-04 contract-test inventory documents ranked CT rows and seeds CT-DEPLOY-01.</summary>
public sealed class Plan04ContractTestsW758LivingSpecTests
{
    [Fact]
    public void Ac1Plan04InventoryDocumentsRankedContractTestsAndSeedsDeployGrpcHost()
    {
        string root = RepoRoot();
        string plan04 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-04-contract-tests.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("PLAN-04 — Contract-test / API Living Spec product tranche", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-DEPLOY-01", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-ZONE-01", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-DRIFT-01", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-AUDIT-01", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-ROUTING-01", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-INCIDENT-01", plan04, StringComparison.Ordinal);
        Assert.Contains("W7-59", plan04, StringComparison.Ordinal);
        // Soft-lock: inventory still lists all ranked CT IDs; host coverage advances in later rows.
        Assert.Contains("DeploymentGrpcHostTests", plan04, StringComparison.Ordinal);
        Assert.Contains("W7-64 DONE", plan04, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-58 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-04", limitations, StringComparison.Ordinal);
        Assert.Contains("CT-DEPLOY-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-59", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-04", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-04-contract-tests.md", continuous, StringComparison.Ordinal);
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
