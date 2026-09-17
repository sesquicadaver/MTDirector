using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-292: PLAN-37 inventory documents sole OPS-HOST-ENV-01 rank (example + docs + bundle) and seeds ENV-01.
/// </summary>
public sealed class Plan37ControllerHostEnvSamplePackagingW7292LivingSpecTests
{
    [Fact]
    public void Ac1Plan37InventoryDocumentsSoleEnvRankAndSeedsOpsHostEnv01()
    {
        string root = RepoRoot();
        string plan37 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-37-controller-host-env-sample-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));

        Assert.Contains("PLAN-37 — Controller host env sample packaging", plan37, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan37, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", plan37, StringComparison.Ordinal);
        Assert.Contains("05212fce", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-294", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-293", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-292", plan37, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan37, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mfc-controller.env.example", plan37, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller", plan37, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-326 (#1058)", plan37, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan37, StringComparison.Ordinal);
        Assert.Contains("bundle", plan37, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Intentional residual (W7-292 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-294", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-293", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-292 | [#991](https://github.com/sesquicadaver/MTDirector/issues/991) | PLAN-37 — Inventory Controller host env sample packaging (mfc-controller.env.example for EnvironmentFile) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-293 | [#992](https://github.com/sesquicadaver/MTDirector/issues/992) | Seed first PLAN-37 atomic row after inventory → OPS-HOST-ENV-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-294 | [#994](https://github.com/sesquicadaver/MTDirector/issues/994) | OPS-HOST-ENV-01 — author mfc-controller.env.example + docs + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-326 (#1058)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-293", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-294", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-37", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-37-controller-host-env-sample-packaging.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-37-controller-host-env-sample-packaging.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan37ControllerHostEnvSamplePackagingW7292", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", packageController, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller/", packaging, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=-/etc/mfc/controller.env", unit, StringComparison.Ordinal);
        Assert.Contains("controller.env", installation, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.service")));
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
