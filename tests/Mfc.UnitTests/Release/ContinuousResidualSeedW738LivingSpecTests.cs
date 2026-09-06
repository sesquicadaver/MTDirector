using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-38: known-limitations / queue seed locks next continuous residual tranche (W7-39 RouterOs fail-closed).</summary>
public sealed class ContinuousResidualSeedW738LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedRouterOsFailClosedResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-38 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("ProbeOnlyRouterOsReadPort", limitations, StringComparison.Ordinal);
        Assert.Contains("NotConfiguredSnapshotCapturePort", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-39", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock RouterOs Enabled default fail-closed residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-39", plan, StringComparison.Ordinal);
        Assert.Contains("RouterOs Enabled default fail-closed", plan, StringComparison.Ordinal);
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
