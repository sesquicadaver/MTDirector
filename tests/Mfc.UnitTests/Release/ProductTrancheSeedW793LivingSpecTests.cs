using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-93: known-limitations / queue seed locks next PLAN-08 row (W7-94 DESK-PROBE-01).</summary>
public sealed class ProductTrancheSeedW793LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskProbe01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan08 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-08-desktop-secondary-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-93 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PROBE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-94", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-94", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-94", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-PROBE-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-92 DONE", plan08, StringComparison.Ordinal);
        Assert.Contains("DESK-PROBE-01", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-94", plan08, StringComparison.Ordinal);
        Assert.Contains("W7-93", roadmap, StringComparison.Ordinal);
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
