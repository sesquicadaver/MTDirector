using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-42: known-limitations / queue seed locks next continuous residual tranche (W7-43 N1-07).</summary>
public sealed class ContinuousResidualSeedW742LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedN107ResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-42 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("N1-07", limitations, StringComparison.Ordinal);
        Assert.Contains("PathClassE2EDriftLivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-43", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock N1-07 path-class E2E DONE residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-43", plan, StringComparison.Ordinal);
        Assert.Contains("N1-07", plan, StringComparison.Ordinal);
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
