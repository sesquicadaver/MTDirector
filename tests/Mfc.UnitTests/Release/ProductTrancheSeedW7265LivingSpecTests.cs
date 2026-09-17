using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-265: seed locked DESK-A11Y-RO-01 (W7-266); opens PLAN-31 COMPLETE follow-up (W7-267).
/// Historical: RO-01 DONE; PLAN-31 COMPLETE; NEXT advanced to W7-268.
/// </summary>
public sealed class ProductTrancheSeedW7265LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yRo01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));

        Assert.Contains("Intentional residual (W7-265 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-RO-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-266", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-267", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-266**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-265 | [#935](https://github.com/sesquicadaver/MTDirector/issues/935) | Seed next PLAN-31 row after DESK-A11Y-LIST-01 → DESK-A11Y-RO-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-266 | [#939](https://github.com/sesquicadaver/MTDirector/issues/939) | DESK-A11Y-RO-01 — Drift SemanticDiff + Audit PayloadJson read-only TextBox AutomationProperties.Name | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-267 | [#940](https://github.com/sesquicadaver/MTDirector/issues/940) | Seed next after DESK-A11Y-RO-01 (PLAN-31 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-346 (#1098)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-265 (#935) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-266", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-RO-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-267", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-346 (#1098)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-265 (#935) DONE", plan31, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-RO-01", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-266 (#939)", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-267 (#940)", plan31, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-346 (#1098)", plan31, StringComparison.Ordinal);
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
