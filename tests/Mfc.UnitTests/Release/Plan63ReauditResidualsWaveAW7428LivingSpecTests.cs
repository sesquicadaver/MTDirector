using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-428 + W7-429: PLAN-63 inventory locks re-audit wave A ranks and seeds OWN-HB-01 as §3.C NEXT.
/// </summary>
public sealed class Plan63ReauditResidualsWaveAW7428LivingSpecTests
{
    [Fact]
    public void Ac1Plan63InventoryDocumentsRanksAndSeedsOwnHb01()
    {
        string root = RepoRoot();
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string reaudit = File.ReadAllText(Path.Combine(root, "docs/audits/MTDirector-reaudit-post-plan62-20261008.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("PLAN-63 — Re-audit residuals wave A", plan63, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-428 (#1253) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-429 (#1254) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-430 (#1255) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-431 (#1256) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-432 (#1257) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-433 (#1258) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", plan63, StringComparison.Ordinal);
        Assert.Contains("EVID-LIVE-01", plan63, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01", plan63, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-435 (#1260)", plan63, StringComparison.Ordinal);
        Assert.Contains("wave B", plan63, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("F01", reaudit, StringComparison.Ordinal);
        Assert.Contains("PARTIAL", reaudit, StringComparison.Ordinal);
        Assert.Contains("OpenEndpointPresence", reaudit, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-428 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-429 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", limitations, StringComparison.Ordinal);
        Assert.Contains("plan-63-reaudit-residuals-wave-a.md", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-428 | [#1253](https://github.com/sesquicadaver/MTDirector/issues/1253) | PLAN-63 — Inventory re-audit residuals wave A (F01/F02/F13) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-429 | [#1254](https://github.com/sesquicadaver/MTDirector/issues/1254) | Seed first PLAN-63 atomic row after inventory → OWN-HB-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-430 | [#1255](https://github.com/sesquicadaver/MTDirector/issues/1255) | OWN-HB-01 — Onboarding lock heartbeat (F01 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-431 | [#1256](https://github.com/sesquicadaver/MTDirector/issues/1256) | Seed next after OWN-HB-01 → EVID-LIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-432 | [#1257](https://github.com/sesquicadaver/MTDirector/issues/1257) | EVID-LIVE-01 — Standalone live Recheck preconditions (F02 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-433 | [#1258](https://github.com/sesquicadaver/MTDirector/issues/1258) | Seed next after EVID-LIVE-01 → M7-PRES-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-435 (#1260)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **4** |", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-63", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-428 (#1253) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-429 (#1254) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-63-reaudit-residuals-wave-a.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-63-reaudit-residuals-wave-a.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan63ReauditResidualsWaveAW7428", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7429", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7431", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7433", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-428` | #1253 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-430` | #1255 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-435 (#1260)", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-435 (#1260)", readme, StringComparison.Ordinal);
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
