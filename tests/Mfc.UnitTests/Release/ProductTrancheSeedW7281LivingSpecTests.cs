using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-281: known-limitations / queue seed locked DESK-HOST-WIN-01 (W7-282) after DESK-HOST-LINUX-01.
/// </summary>
public sealed class ProductTrancheSeedW7281LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskHostWin01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));

        Assert.Contains("Intentional residual (W7-281 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-WIN-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-282", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-283", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-282**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-281 | [#967](https://github.com/sesquicadaver/MTDirector/issues/967) | Seed next PLAN-34 row after DESK-HOST-LINUX-01 → DESK-HOST-WIN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-282 | [#971](https://github.com/sesquicadaver/MTDirector/issues/971) | DESK-HOST-WIN-01 — Windows Start Menu shortcut sketch for framework-dependent Desktop | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-283 | [#972](https://github.com/sesquicadaver/MTDirector/issues/972) | Seed next after DESK-HOST-WIN-01 (PLAN-34 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-282 (#971)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-281 (#967) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-282", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-WIN-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-282 (#971)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-281 (#967) DONE", plan34, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-WIN-01", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-282", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-283", plan34, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-282 (#971)", plan34, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-desktop-start-menu.ps1", plan34, StringComparison.Ordinal);
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
