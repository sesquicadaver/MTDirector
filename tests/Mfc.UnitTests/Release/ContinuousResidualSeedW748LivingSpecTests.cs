using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-48: known-limitations / queue seed locks next continuous residual tranche (W7-49 corpus COMPLETE).</summary>
public sealed class ContinuousResidualSeedW748LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedResidualCorpusCompleteAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-48 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("corpus COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("Living-Spec locked", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-49", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock known-limitations residual Living Spec corpus COMPLETE", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-49", plan, StringComparison.Ordinal);
        Assert.Contains("corpus COMPLETE", plan, StringComparison.Ordinal);
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
