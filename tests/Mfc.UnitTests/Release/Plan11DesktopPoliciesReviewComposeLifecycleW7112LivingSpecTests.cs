using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-112: PLAN-11 Desktop Policies review-compose lifecycle inventory documents ranked DESK-* rows and seeds DESK-SUBMIT-01.</summary>
public sealed class Plan11DesktopPoliciesReviewComposeLifecycleW7112LivingSpecTests
{
    [Fact]
    public void Ac1Plan11InventoryDocumentsRankedRowsAndSeedsDeskSubmit01()
    {
        string root = RepoRoot();
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-11 — Desktop Policies review-compose lifecycle Living Spec product tranche", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-SUBMIT-01", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-COMPOSE-01", plan11, StringComparison.Ordinal);
        Assert.Contains("DESK-GATE-01", plan11, StringComparison.Ordinal);
        Assert.Contains("W7-114", plan11, StringComparison.Ordinal);
        Assert.Contains("SubmitCommand", plan11, StringComparison.Ordinal);
        Assert.Contains("SubmitForReviewAsync", plan11, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-112 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-SUBMIT-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-114", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-11-desktop-policies-review-compose-lifecycle.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-11-desktop-policies-review-compose-lifecycle.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan10, StringComparison.Ordinal);
        Assert.Contains("PLAN-11", plan10, StringComparison.Ordinal);
        Assert.Contains("Plan11DesktopPoliciesReviewComposeLifecycleW7112", testing, StringComparison.Ordinal);
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
