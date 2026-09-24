using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-283: PLAN-34 COMPLETE; known-limitations / queue seed locked PLAN-35 inventory (W7-284 DONE)
/// and follow-up seed W7-285 after DESK-HOST-WIN-01.
/// </summary>
public sealed class ProductTrancheSeedW7283LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan35AfterPlan34Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));
        string plan35 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-35-desktop-launch-template-publish-bundling.md"));
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));

        Assert.Contains("Intentional residual (W7-283 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-34 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-35", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-284", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-285", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-284**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-283 | [#972](https://github.com/sesquicadaver/MTDirector/issues/972) | Seed next after DESK-HOST-WIN-01 (PLAN-34 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-284 | [#975](https://github.com/sesquicadaver/MTDirector/issues/975) | PLAN-35 — Inventory Desktop launch-template publish bundling (package-desktop copies templates into OUT_DIR/desktop) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-285 | [#976](https://github.com/sesquicadaver/MTDirector/issues/976) | Seed first PLAN-35 atomic row after inventory → DESK-HOST-BUNDLE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-34 COMPLETE", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-283 (#972) DONE", plan34, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", plan34, StringComparison.Ordinal);
        Assert.Contains("plan-35-desktop-launch-template-publish-bundling.md", plan34, StringComparison.Ordinal);

        Assert.Contains("PLAN-35", plan, StringComparison.Ordinal);
        Assert.Contains("W7-284", plan, StringComparison.Ordinal);
        Assert.Contains("W7-283 (#972) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-35-desktop-launch-template-publish-bundling.md", plan, StringComparison.Ordinal);

        Assert.Contains("DESK-HOST-BUNDLE-01", plan35, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-284", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-285", plan35, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", plan35, StringComparison.Ordinal);
        Assert.Contains("package-desktop.sh", plan35, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/desktop", plan35, StringComparison.Ordinal);

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
