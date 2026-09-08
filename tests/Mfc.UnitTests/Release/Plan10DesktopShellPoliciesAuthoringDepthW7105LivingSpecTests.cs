using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-105: PLAN-10 Desktop shell chrome & Policies authoring depth inventory documents ranked DESK-* rows and seeds DESK-SHELL-01.</summary>
public sealed class Plan10DesktopShellPoliciesAuthoringDepthW7105LivingSpecTests
{
    [Fact]
    public void Ac1Plan10InventoryDocumentsRankedRowsAndSeedsDeskShell01()
    {
        string root = RepoRoot();
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("PLAN-10 — Desktop shell chrome & Policies authoring depth Living Spec product tranche", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-SHELL-01", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-DIFF-01", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-REORDER-01", plan10, StringComparison.Ordinal);
        Assert.Contains("W7-106", plan10, StringComparison.Ordinal);
        Assert.Contains("ShellViewModel", plan10, StringComparison.Ordinal);
        Assert.Contains("SelectModuleCommand", plan10, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-105 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-10", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-SHELL-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-106", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-10", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-10-desktop-shell-policies-authoring-depth.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-10-desktop-shell-policies-authoring-depth.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan09, StringComparison.Ordinal);
        Assert.Contains("PLAN-10", plan09, StringComparison.Ordinal);
        Assert.Contains("Plan10DesktopShellPoliciesAuthoringDepthW7105", testing, StringComparison.Ordinal);
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
