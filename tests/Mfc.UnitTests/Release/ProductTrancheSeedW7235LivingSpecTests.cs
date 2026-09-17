using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-235: known-limitations / queue seed locked AUDIT-INT-01 (W7-236) after AUDIT-AUTH-01.
/// Historical seed residue: locked AUDIT-INT-01 queue after AUDIT-AUTH-01; NEXT later advanced past W7-239.
/// </summary>
public sealed class ProductTrancheSeedW7235LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedAuditInt01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));

        Assert.Contains("Intentional residual (W7-235 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-INT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-236", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-237", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-26 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-236", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-236 | [#879](https://github.com/sesquicadaver/MTDirector/issues/879) | AUDIT-INT-01 — FastTrack topology / verification session disposal / progress Watch auth hubs | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-237 | [#880](https://github.com/sesquicadaver/MTDirector/issues/880) | Seed next after AUDIT-INT-01 (PLAN-26 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-235 | [#876](https://github.com/sesquicadaver/MTDirector/issues/876) | Seed next PLAN-26 row after AUDIT-AUTH-01 → AUDIT-INT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("W7-236", plan, StringComparison.Ordinal);
        Assert.Contains("AUDIT-INT-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-235 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AUTH-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-236", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-237", plan26, StringComparison.Ordinal);
        Assert.Contains("rank 14", plan26, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seeded as **W7-236**", limitations, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-305 (#1016)", roadmap, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-305 (#1016)", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-237 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-236 (#879) DONE", plan26, StringComparison.Ordinal);
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
