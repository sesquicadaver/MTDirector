using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-171: known-limitations / queue seed locks next PLAN-18 row (W7-172 DESK-A11Y-INGEST-02).</summary>
public sealed class ProductTrancheSeedW7171LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yIngest02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan18 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-18-desktop-incident-ingest-action-automation.md"));

        Assert.Contains("Intentional residual (W7-171 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-172", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-172", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-02 — Ingest Name + Bind Name + Incident field Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-172", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-171 DONE", plan18, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INGEST-02", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-172", plan18, StringComparison.Ordinal);
        Assert.Contains("W7-171", roadmap, StringComparison.Ordinal);
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
