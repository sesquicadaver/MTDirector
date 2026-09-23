using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-393 + W7-394: PLAN-62 inventory locks audit remediation ranks and seeds
/// AUDIT-STATUS-01 as §3.C NEXT.
/// </summary>
public sealed class Plan62AuditRemediationAcD0759W7393LivingSpecTests
{
    [Fact]
    public void Ac1Plan62InventoryDocumentsRanksAndSeedsAuditStatus01()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string audit = File.ReadAllText(Path.Combine(root, "docs/audits/MTDirector-audit-acd0759-20260923.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("PLAN-62 — Repository-audit remediation", plan62, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-393 (#1195) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-394 (#1196) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-STATUS-01", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-COMMIT-01", plan62, StringComparison.Ordinal);
        Assert.Contains("acd0759e85414a83460c4cab971db2b0b58b30cd", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", plan62, StringComparison.Ordinal);

        Assert.Contains("acd0759e85414a83460c4cab971db2b0b58b30cd", audit, StringComparison.Ordinal);
        Assert.Contains("F01", audit, StringComparison.Ordinal);
        Assert.Contains("AllSafeEvidence", audit, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-393 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-394 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-STATUS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("plan-62-audit-remediation-acd0759.md", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-393 | [#1195](https://github.com/sesquicadaver/MTDirector/issues/1195) | PLAN-62 — Inventory repository-audit remediation (acd0759) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-394 | [#1196](https://github.com/sesquicadaver/MTDirector/issues/1196) | Seed first PLAN-62 atomic row after inventory → AUDIT-STATUS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-395 | [#1197](https://github.com/sesquicadaver/MTDirector/issues/1197) | AUDIT-STATUS-01 — Honest MVP/M7/write-path status vs audit acd0759 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-397 | [#1200](https://github.com/sesquicadaver/MTDirector/issues/1200) | AUDIT-SBOM-01 — SBOM/signing fail-closed (no empty components / missing SDK) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **1** |", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-62", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-393 (#1195) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-394 (#1196) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-62-audit-remediation-acd0759.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-62-audit-remediation-acd0759.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan62AuditRemediationAcD0759W7393", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7394", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-393` | #1195 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-394` | #1196 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-395` | #1197 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", issues, StringComparison.Ordinal);
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
