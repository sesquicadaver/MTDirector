using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-263: known-limitations / queue seed locked DESK-A11Y-LIST-01 (W7-264) after PLAN-31 inventory.
/// Historical: LIST-01 DONE; RO seed DONE; NEXT advanced to W7-266 RO implement.
/// </summary>
public sealed class ProductTrancheSeedW7263LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yList01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));

        Assert.Contains("Intentional residual (W7-263 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-LIST-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-264", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-265", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-264**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-263 | [#932](https://github.com/sesquicadaver/MTDirector/issues/932) | Seed first PLAN-31 atomic row after inventory → DESK-A11Y-LIST-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-264 | [#934](https://github.com/sesquicadaver/MTDirector/issues/934) | DESK-A11Y-LIST-01 — ListBox host AutomationProperties.Name across operator browse/select surfaces | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-265 | [#935](https://github.com/sesquicadaver/MTDirector/issues/935) | Seed next PLAN-31 row after DESK-A11Y-LIST-01 → DESK-A11Y-RO-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-263 (#932) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-264", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-LIST-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-263 (#932) DONE", plan31, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-LIST-01", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-264", plan31, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-265", plan31, StringComparison.Ordinal);
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
