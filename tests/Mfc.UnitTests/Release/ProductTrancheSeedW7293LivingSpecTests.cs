using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-293: known-limitations / queue seed locked OPS-HOST-ENV-01 (W7-294) after PLAN-37 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7293LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostEnv01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan37 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-37-controller-host-env-sample-packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));

        Assert.Contains("Intentional residual (W7-293 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-294", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-295", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-294**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-293 | [#992](https://github.com/sesquicadaver/MTDirector/issues/992) | Seed first PLAN-37 atomic row after inventory → OPS-HOST-ENV-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-294 | [#994](https://github.com/sesquicadaver/MTDirector/issues/994) | OPS-HOST-ENV-01 — author mfc-controller.env.example + docs + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-295 | [#996](https://github.com/sesquicadaver/MTDirector/issues/996) | Seed next after OPS-HOST-ENV-01 (PLAN-37 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-293 (#992) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-294", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-293 (#992) DONE", plan37, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-294", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-295", plan37, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", plan37, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
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
