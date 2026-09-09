using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-161: known-limitations / queue seed locks next PLAN-16 row (W7-162 DESK-A11Y-02).</summary>
public sealed class ProductTrancheSeedW7161LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11y02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan16 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-16-desktop-incident-automation-properties.md"));

        Assert.Contains("Intentional residual (W7-161 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-162", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-162", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-02 — Incident Names + PlaceholderText + mfc-field regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-162", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-161 DONE", plan16, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-02", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-162", plan16, StringComparison.Ordinal);
        Assert.Contains("W7-161", roadmap, StringComparison.Ordinal);
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
