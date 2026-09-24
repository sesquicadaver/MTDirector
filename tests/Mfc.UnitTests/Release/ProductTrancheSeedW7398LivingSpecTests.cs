using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-398: after AUDIT-SBOM-01, AUDIT-OWN-01 was seeded (W7-399);
/// queue may have advanced past that row.
/// </summary>
public sealed class ProductTrancheSeedW7398LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditOwn01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("Intentional residual (W7-398 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-399", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-398 | [#1202](https://github.com/sesquicadaver/MTDirector/issues/1202) | Seed next after AUDIT-SBOM-01 → AUDIT-OWN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-399 | [#1203](https://github.com/sesquicadaver/MTDirector/issues/1203) | AUDIT-OWN-01 — Onboarding durable writer lease vs recovery | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-398 (#1202) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-399 (#1203)", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", readme, StringComparison.Ordinal);
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
