using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-176: known-limitations / queue seed locks next PLAN-19 row (W7-177 DESK-A11Y-CONN-02).</summary>
public sealed class ProductTrancheSeedW7176LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yConn02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan19 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-19-desktop-shell-connect-disconnect-automation.md"));

        Assert.Contains("Intentional residual (W7-176 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-177", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-177", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-02 — Connect/Disconnect Names + Incident action Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-177", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-176 DONE", plan19, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-CONN-02", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-177", plan19, StringComparison.Ordinal);
        Assert.Contains("W7-176", roadmap, StringComparison.Ordinal);
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
