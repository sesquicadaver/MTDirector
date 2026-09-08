using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-100: known-limitations / queue seed locks next PLAN-09 row (W7-101 DESK-MTLS-01).</summary>
public sealed class ProductTrancheSeedW7100LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskMtls01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-100 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-MTLS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-101", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-101", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-101", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-MTLS-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-99 DONE", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-MTLS-01", plan09, StringComparison.Ordinal);
        Assert.Contains("W7-101", plan09, StringComparison.Ordinal);
        Assert.Contains("W7-100", roadmap, StringComparison.Ordinal);
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
