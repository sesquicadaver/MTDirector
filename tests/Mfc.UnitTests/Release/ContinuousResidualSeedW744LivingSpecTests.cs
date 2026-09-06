using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-44: known-limitations / queue seed locks next continuous residual tranche (W7-45 M7 CLOSED).</summary>
public sealed class ContinuousResidualSeedW744LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedM7ClosedResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-44 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("M7.1…M7.4", limitations, StringComparison.Ordinal);
        Assert.Contains("v0.2.0", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-45", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock M7.1…M7.4 CLOSED residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-45", plan, StringComparison.Ordinal);
        Assert.Contains("M7.1…M7.4", plan, StringComparison.Ordinal);
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
