using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-156: known-limitations / queue seed locks next PLAN-15 row (W7-157 DESK-FIELD-02).</summary>
public sealed class ProductTrancheSeedW7156LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskField02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan15 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-15-desktop-incident-mfc-field-style-hygiene.md"));

        Assert.Contains("Intentional residual (W7-156 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-157", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-157", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-02 — Incident PlaceholderText + mfc-field regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-157", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-156 DONE", plan15, StringComparison.Ordinal);
        Assert.Contains("DESK-FIELD-02", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-157", plan15, StringComparison.Ordinal);
        Assert.Contains("W7-156", roadmap, StringComparison.Ordinal);
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
