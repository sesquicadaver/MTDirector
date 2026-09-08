using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-119: PLAN-12 Desktop Policies residual lifecycle inventory documents ranked DESK-* rows and seeds DESK-ACK-01.</summary>
public sealed class Plan12DesktopPoliciesResidualLifecycleW7119LivingSpecTests
{
    [Fact]
    public void Ac1Plan12InventoryDocumentsRankedRowsAndSeedsDeskAck01()
    {
        string root = RepoRoot();
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan11 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-11-desktop-policies-review-compose-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-12 — Desktop Policies residual lifecycle Living Spec product tranche", plan12, StringComparison.Ordinal);
        Assert.Contains("DESK-ACK-01", plan12, StringComparison.Ordinal);
        Assert.Contains("DESK-DRAFT-01", plan12, StringComparison.Ordinal);
        Assert.Contains("DESK-CATALOG-01", plan12, StringComparison.Ordinal);
        Assert.Contains("W7-120", plan12, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeWarningCommand", plan12, StringComparison.Ordinal);
        Assert.Contains("AcknowledgeWarningAsync", plan12, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-119 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-ACK-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-120", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-12-desktop-policies-residual-lifecycle.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-12-desktop-policies-residual-lifecycle.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan11, StringComparison.Ordinal);
        Assert.Contains("PLAN-12", plan11, StringComparison.Ordinal);
        Assert.Contains("Plan12DesktopPoliciesResidualLifecycleW7119", testing, StringComparison.Ordinal);
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
