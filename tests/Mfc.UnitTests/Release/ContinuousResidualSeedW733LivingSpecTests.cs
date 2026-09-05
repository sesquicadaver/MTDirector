using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-33: known-limitations / queue seed locks next continuous residual tranche (W7-34 Desktop window).</summary>
public sealed class ContinuousResidualSeedW733LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDesktopWindowResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-33 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("Closing the Desktop window **does not** stop Controller", limitations, StringComparison.Ordinal);
        Assert.Contains("separate OS processes", limitations, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-34 (#466)", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock Desktop window does not stop Controller residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-34", plan, StringComparison.Ordinal);
        Assert.Contains("Desktop window does not stop Controller", plan, StringComparison.Ordinal);
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
