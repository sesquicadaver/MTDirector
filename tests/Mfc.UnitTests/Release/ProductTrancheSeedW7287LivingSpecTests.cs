using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-287: PLAN-35 COMPLETE; known-limitations / queue seed locked PLAN-36 inventory (W7-288)
/// and follow-up seed W7-289 after DESK-HOST-BUNDLE-01.
/// Historical: inventory W7-288 now DONE; §3.C NEXT advanced to W7-289.
/// </summary>
public sealed class ProductTrancheSeedW7287LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan36AfterPlan35Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan35 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-35-desktop-launch-template-publish-bundling.md"));
        string plan36 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-36-controller-host-template-publish-bundling.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));

        Assert.Contains("Intentional residual (W7-287 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-35 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-36", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-288", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-289", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-288**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-287 | [#979](https://github.com/sesquicadaver/MTDirector/issues/979) | Seed next after DESK-HOST-BUNDLE-01 (PLAN-35 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-288 | [#983](https://github.com/sesquicadaver/MTDirector/issues/983) | PLAN-36 — Inventory Controller host-template publish bundling (package-controller copies systemd/WinSW into OUT_DIR/controller) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-289 | [#984](https://github.com/sesquicadaver/MTDirector/issues/984) | Seed first PLAN-36 atomic row after inventory → OPS-HOST-BUNDLE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-35 COMPLETE", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-287 (#979) DONE", plan35, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", plan35, StringComparison.Ordinal);
        Assert.Contains("plan-36-controller-host-template-publish-bundling.md", plan35, StringComparison.Ordinal);

        Assert.Contains("PLAN-36", plan, StringComparison.Ordinal);
        Assert.Contains("W7-288", plan, StringComparison.Ordinal);
        Assert.Contains("W7-291 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-36-controller-host-template-publish-bundling.md", plan, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-BUNDLE-01", plan36, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-288", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-289", plan36, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", plan36, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan36, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller", plan36, StringComparison.Ordinal);
        Assert.Contains("621f13f3", plan36, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.winsw.xml", packageController, StringComparison.Ordinal);
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
