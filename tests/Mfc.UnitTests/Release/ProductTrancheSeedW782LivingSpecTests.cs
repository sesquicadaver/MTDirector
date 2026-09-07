using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-82: known-limitations / queue seed locks next PLAN-07 row (W7-83 DESK-ONBOARD-01).</summary>
public sealed class ProductTrancheSeedW782LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskOnboard01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));

        Assert.Contains("Intentional residual (W7-82 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-ONBOARD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-83", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-83", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-83", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-ONBOARD-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-81 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-ONBOARD-01", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-83", plan07, StringComparison.Ordinal);
        Assert.Contains("W7-82", roadmap, StringComparison.Ordinal);
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
