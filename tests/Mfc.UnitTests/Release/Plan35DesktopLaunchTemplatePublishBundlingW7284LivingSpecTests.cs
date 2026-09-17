using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-284: PLAN-35 inventory documents sole DESK-HOST-BUNDLE-01 rank and seeds BUNDLE-01.
/// </summary>
public sealed class Plan35DesktopLaunchTemplatePublishBundlingW7284LivingSpecTests
{
    [Fact]
    public void Ac1Plan35InventoryDocumentsSoleBundleRankAndSeedsDeskHostBundle01()
    {
        string root = RepoRoot();
        string plan35 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-35-desktop-launch-template-publish-bundling.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));

        Assert.Contains("PLAN-35 — Desktop launch-template publish bundling (package-desktop → OUT_DIR/desktop)", plan35, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan35, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", plan35, StringComparison.Ordinal);
        Assert.Contains("d461b82", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-286", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-287", plan35, StringComparison.Ordinal);
        Assert.Contains("W7-285", plan35, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan35, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OUT_DIR/desktop", plan35, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-340 (#1087)", plan35, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan35, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-284 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-286", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-287", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-284 | [#975](https://github.com/sesquicadaver/MTDirector/issues/975) | PLAN-35 — Inventory Desktop launch-template publish bundling (package-desktop copies templates into OUT_DIR/desktop) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-340 (#1087)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-285", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-286", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-35", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-35-desktop-launch-template-publish-bundling.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-35-desktop-launch-template-publish-bundling.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan35DesktopLaunchTemplatePublishBundlingW7284", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop.desktop", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop-start-menu.ps1", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("package-desktop.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/desktop/", packaging, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/linux/mfc-desktop.desktop")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/windows/mfc-desktop-start-menu.ps1")));
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
