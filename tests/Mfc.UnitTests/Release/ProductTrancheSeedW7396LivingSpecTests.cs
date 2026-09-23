using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-396: after AUDIT-STATUS-01, §3.C NEXT is AUDIT-SBOM-01 (W7-397).
/// </summary>
public sealed class ProductTrancheSeedW7396LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditSbom01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-396 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-397", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-SBOM-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-396 | [#1199](https://github.com/sesquicadaver/MTDirector/issues/1199) | Seed next after AUDIT-STATUS-01 → AUDIT-SBOM-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-397 | [#1200](https://github.com/sesquicadaver/MTDirector/issues/1200) | AUDIT-SBOM-01 — SBOM/signing fail-closed (no empty components / missing SDK) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-397 (#1200)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-396 (#1199) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-397 (#1200)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-SBOM-01", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-397 (#1200)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-397 (#1200)", readme, StringComparison.Ordinal);
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
