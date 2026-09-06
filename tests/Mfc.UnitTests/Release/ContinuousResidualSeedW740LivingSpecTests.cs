using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-40: known-limitations / queue seed locks next continuous residual tranche (W7-41 WriteEnabled).</summary>
public sealed class ContinuousResidualSeedW740LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedWriteEnabledResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-40 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("Mfc:RouterOs:WriteEnabled=true", limitations, StringComparison.Ordinal);
        Assert.Contains("P2-11", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-41", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock RouterOs WriteEnabled operator residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-41", plan, StringComparison.Ordinal);
        Assert.Contains("WriteEnabled", plan, StringComparison.Ordinal);
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
