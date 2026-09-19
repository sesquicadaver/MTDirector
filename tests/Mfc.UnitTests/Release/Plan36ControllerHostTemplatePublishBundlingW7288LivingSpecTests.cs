using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-288: PLAN-36 inventory documents sole OPS-HOST-BUNDLE-01 rank and seeds BUNDLE-01.
/// </summary>
public sealed class Plan36ControllerHostTemplatePublishBundlingW7288LivingSpecTests
{
    [Fact]
    public void Ac1Plan36InventoryDocumentsSoleBundleRankAndSeedsOpsHostBundle01()
    {
        string root = RepoRoot();
        string plan36 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-36-controller-host-template-publish-bundling.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));

        Assert.Contains("PLAN-36 — Controller host-template publish bundling (package-controller → OUT_DIR/controller)", plan36, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan36, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", plan36, StringComparison.Ordinal);
        Assert.Contains("621f13f3", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-290", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-291", plan36, StringComparison.Ordinal);
        Assert.Contains("W7-289", plan36, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan36, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OUT_DIR/controller", plan36, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-382 (#1170)", plan36, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan36, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-288 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-290", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-291", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-288 | [#983](https://github.com/sesquicadaver/MTDirector/issues/983) | PLAN-36 — Inventory Controller host-template publish bundling (package-controller copies systemd/WinSW into OUT_DIR/controller) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-382 (#1170)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-289", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-290", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-36", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-36-controller-host-template-publish-bundling.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-36-controller-host-template-publish-bundling.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan36ControllerHostTemplatePublishBundlingW7288", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.winsw.xml", packageController, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller/", packaging, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.service")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/windows/mfc-controller.winsw.xml")));
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
