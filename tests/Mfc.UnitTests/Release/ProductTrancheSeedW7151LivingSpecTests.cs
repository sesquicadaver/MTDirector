using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-151: known-limitations / queue seed locks next PLAN-14 row (W7-152 DESK-PLACEHOLDER-02).</summary>
public sealed class ProductTrancheSeedW7151LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskPlaceholder02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan14 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-14-desktop-avalonia-placeholder-incident-surface.md"));

        Assert.Contains("Intentional residual (W7-151 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-152", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-152", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-02 — Repo-wide Desktop XAML Watermark residue Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-152", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-151 DONE", plan14, StringComparison.Ordinal);
        Assert.Contains("DESK-PLACEHOLDER-02", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-152", plan14, StringComparison.Ordinal);
        Assert.Contains("W7-151", roadmap, StringComparison.Ordinal);
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
