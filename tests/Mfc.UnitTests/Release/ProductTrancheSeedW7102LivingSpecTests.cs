using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-102: known-limitations / queue seed locks next PLAN-09 row (W7-103 DESK-AUTH-01).</summary>
public sealed class ProductTrancheSeedW7102LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskAuth01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-102 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-AUTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-103", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-103", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-103", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-AUTH-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-101 DONE", plan09, StringComparison.Ordinal);
        Assert.Contains("DESK-AUTH-01", plan09, StringComparison.Ordinal);
        Assert.Contains("W7-103", plan09, StringComparison.Ordinal);
        Assert.Contains("W7-102", roadmap, StringComparison.Ordinal);
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
