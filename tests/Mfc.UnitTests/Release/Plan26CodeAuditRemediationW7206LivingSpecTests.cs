using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-206: PLAN-26 inventory documents ranked AUDIT-* rows and seeds AUDIT-RULE-01.</summary>
public sealed class Plan26CodeAuditRemediationW7206LivingSpecTests
{
    [Fact]
    public void Ac1Plan26InventoryDocumentsRankedRowsAndSeedsAuditRule01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string audit = File.ReadAllText(Path.Combine(root, "docs/audits/MTDirector-audit-11cb746-20260911.md"));

        Assert.Contains("PLAN-26 — Code-audit remediation tranche", plan26, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RULE-01", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-210", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-211", plan26, StringComparison.Ordinal);
        Assert.Contains("PoliciesViewModel", plan26, StringComparison.Ordinal);
        Assert.Contains("PolicyRuleFactory", plan26, StringComparison.Ordinal);
        Assert.Contains("11cb746de60191e6eb83e52013f7f544306d5c9d", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-206 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RULE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RULE-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-210", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-210", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-26", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-26-code-audit-remediation-11cb746.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-26-code-audit-remediation-11cb746.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan26CodeAuditRemediationW7206", testing, StringComparison.Ordinal);
        Assert.Contains("Update rule втрачає predicate", audit, StringComparison.Ordinal);
        Assert.Contains("11cb746de60191e6eb83e52013f7f544306d5c9d", audit, StringComparison.Ordinal);
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
