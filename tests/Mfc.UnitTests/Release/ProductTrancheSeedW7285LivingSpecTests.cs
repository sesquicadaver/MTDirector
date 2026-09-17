using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-285: known-limitations / queue seed locked DESK-HOST-BUNDLE-01 (W7-286) after PLAN-35 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7285LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskHostBundle01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan35 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-35-desktop-launch-template-publish-bundling.md"));
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));

        Assert.Contains("Intentional residual (W7-285 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-286", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-287", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-286**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-285 | [#976](https://github.com/sesquicadaver/MTDirector/issues/976) | Seed first PLAN-35 atomic row after inventory → DESK-HOST-BUNDLE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-286 | [#978](https://github.com/sesquicadaver/MTDirector/issues/978) | DESK-HOST-BUNDLE-01 — package-desktop copies launch templates into OUT_DIR/desktop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-287 | [#979](https://github.com/sesquicadaver/MTDirector/issues/979) | Seed next after DESK-HOST-BUNDLE-01 (PLAN-35 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-293 (#992)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-285 (#976) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-286", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-293 (#992)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-285 (#976) DONE", plan35, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-286", plan35, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-293 (#992)", plan35, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop.desktop", packageDesktop, StringComparison.Ordinal);
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
