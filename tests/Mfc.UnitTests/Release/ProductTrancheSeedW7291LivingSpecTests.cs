using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-291: PLAN-36 COMPLETE; known-limitations / queue seed locked PLAN-37 inventory (W7-292)
/// and follow-up seed W7-293 after OPS-HOST-BUNDLE-01.
/// Historical: inventory W7-292 now DONE; §3.C NEXT advanced to W7-293.
/// </summary>
public sealed class ProductTrancheSeedW7291LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan37AfterPlan36Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan36 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-36-controller-host-template-publish-bundling.md"));
        string plan37 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-37-controller-host-env-sample-packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));

        Assert.Contains("Intentional residual (W7-291 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-36 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-37", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-292", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-293", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-292**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-291 | [#987](https://github.com/sesquicadaver/MTDirector/issues/987) | Seed next after OPS-HOST-BUNDLE-01 (PLAN-36 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-292 | [#991](https://github.com/sesquicadaver/MTDirector/issues/991) | PLAN-37 — Inventory Controller host env sample packaging (mfc-controller.env.example for EnvironmentFile) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-293 | [#992](https://github.com/sesquicadaver/MTDirector/issues/992) | Seed first PLAN-37 atomic row after inventory → OPS-HOST-ENV-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-297 (#1000)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-36 COMPLETE", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-291 (#987) DONE", plan36, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-297 (#1000)", plan36, StringComparison.Ordinal);
        Assert.Contains("plan-37-controller-host-env-sample-packaging.md", plan36, StringComparison.Ordinal);

        Assert.Contains("PLAN-37", plan, StringComparison.Ordinal);
        Assert.Contains("W7-292", plan, StringComparison.Ordinal);
        Assert.Contains("W7-291 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-297 (#1000)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-37-controller-host-env-sample-packaging.md", plan, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-ENV-01", plan37, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-292", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-293", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-294", plan37, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-297 (#1000)", plan37, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", plan37, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile", plan37, StringComparison.Ordinal);
        Assert.Contains("05212fce", plan37, StringComparison.Ordinal);

        Assert.Contains("EnvironmentFile=-/etc/mfc/controller.env", unit, StringComparison.Ordinal);
        Assert.Contains("mfc_controller_bundle_host_templates", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", packageController, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.env.example")));
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
