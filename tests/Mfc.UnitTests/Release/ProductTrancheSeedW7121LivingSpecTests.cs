using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-121: known-limitations / queue seed locks next PLAN-12 row (W7-122 DESK-DRAFT-01).</summary>
public sealed class ProductTrancheSeedW7121LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskDraft01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));

        Assert.Contains("Intentional residual (W7-121 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-DRAFT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-122", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-122", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-DRAFT-01 — Desktop Policies Create/Load draft Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-122", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-DRAFT-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-120 DONE", plan12, StringComparison.Ordinal);
        Assert.Contains("DESK-DRAFT-01", plan12, StringComparison.Ordinal);
        Assert.Contains("W7-122", plan12, StringComparison.Ordinal);
        Assert.Contains("W7-121", roadmap, StringComparison.Ordinal);
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
