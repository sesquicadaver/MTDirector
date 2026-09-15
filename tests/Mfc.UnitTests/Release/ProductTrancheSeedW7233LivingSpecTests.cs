using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-233: known-limitations / queue seed locks AUDIT-AUTH-01 (W7-234) after AUDIT-GUI-01.</summary>
public sealed class ProductTrancheSeedW7233LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditAuth01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-233 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AUTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-234", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-235", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-INT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-234", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-234 | [#875](https://github.com/sesquicadaver/MTDirector/issues/875) | AUDIT-AUTH-01 — Production operator authorization DenyAll | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-233 | [#872](https://github.com/sesquicadaver/MTDirector/issues/872) | Seed next PLAN-26 row after AUDIT-GUI-01 → AUDIT-AUTH-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("W7-234", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AUTH-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-233 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-234", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-235", plan26, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-234**", limitations, StringComparison.Ordinal);
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
