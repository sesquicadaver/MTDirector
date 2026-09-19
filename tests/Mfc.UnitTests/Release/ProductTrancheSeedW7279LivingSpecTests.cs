using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-279: known-limitations / queue seed locked DESK-HOST-LINUX-01 (W7-280) after PLAN-34 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7279LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskHostLinux01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));

        Assert.Contains("Intentional residual (W7-279 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-280", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-281", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-280**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-279 | [#964](https://github.com/sesquicadaver/MTDirector/issues/964) | Seed first PLAN-34 atomic row after inventory → DESK-HOST-LINUX-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-280 | [#966](https://github.com/sesquicadaver/MTDirector/issues/966) | DESK-HOST-LINUX-01 — freedesktop .desktop template for framework-dependent Desktop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-281 | [#967](https://github.com/sesquicadaver/MTDirector/issues/967) | Seed next PLAN-34 row after DESK-HOST-LINUX-01 → DESK-HOST-WIN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-364 (#1135)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-279 (#964) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-280", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-364 (#1135)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-279 (#964) DONE", plan34, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-280", plan34, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-364 (#1135)", plan34, StringComparison.Ordinal);
        Assert.Contains("packaging/linux/mfc-desktop.desktop", plan34, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageDesktop, StringComparison.Ordinal);
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
