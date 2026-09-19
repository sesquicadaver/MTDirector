using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-289: known-limitations / queue seed locked OPS-HOST-BUNDLE-01 (W7-290) after PLAN-36 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7289LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostBundle01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan36 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-36-controller-host-template-publish-bundling.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));

        Assert.Contains("Intentional residual (W7-289 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-290", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-291", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-290**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-289 | [#984](https://github.com/sesquicadaver/MTDirector/issues/984) | Seed first PLAN-36 atomic row after inventory → OPS-HOST-BUNDLE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-290 | [#986](https://github.com/sesquicadaver/MTDirector/issues/986) | OPS-HOST-BUNDLE-01 — package-controller copies systemd/WinSW into OUT_DIR/controller | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-291 | [#987](https://github.com/sesquicadaver/MTDirector/issues/987) | Seed next after OPS-HOST-BUNDLE-01 (PLAN-36 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-375 (#1155)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-289 (#984) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-290", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-375 (#1155)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-289 (#984) DONE", plan36, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-290", plan36, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-375 (#1155)", plan36, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
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
