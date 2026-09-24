using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-278: PLAN-34 inventory documents ranked DESK-HOST-LINUX/WIN rows and seeds LINUX-01.
/// </summary>
public sealed class Plan34DesktopOperatorLaunchPackagingW7278LivingSpecTests
{
    [Fact]
    public void Ac1Plan34InventoryDocumentsRankedRowsAndSeedsDeskHostLinux01()
    {
        string root = RepoRoot();
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));

        Assert.Contains("PLAN-34 — Desktop operator launch packaging templates (.desktop / Windows shortcut)", plan34, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan34, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01", plan34, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-WIN-01", plan34, StringComparison.Ordinal);
        Assert.Contains("3e112bf", plan34, StringComparison.Ordinal);
        Assert.Contains("packaging/linux/mfc-desktop.desktop", plan34, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-desktop-start-menu.ps1", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-280", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-281", plan34, StringComparison.Ordinal);
        Assert.Contains("W7-279", plan34, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", plan34, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/desktop", plan34, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", plan34, StringComparison.Ordinal);
        Assert.Contains("WIN-01 kept", plan34, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-278 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-280", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-WIN-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-278 | [#963](https://github.com/sesquicadaver/MTDirector/issues/963) | PLAN-34 — Inventory Desktop operator launch packaging templates (.desktop / Windows shortcut) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains(
            "W7-282 | [#971](https://github.com/sesquicadaver/MTDirector/issues/971) | DESK-HOST-WIN-01 — Windows Start Menu shortcut sketch for framework-dependent Desktop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-283 | [#972](https://github.com/sesquicadaver/MTDirector/issues/972) | Seed next after DESK-HOST-WIN-01 (PLAN-34 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-279", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-280", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-34", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-34-desktop-operator-launch-packaging.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-34-desktop-operator-launch-packaging.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan34DesktopOperatorLaunchPackagingW7278", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("package-desktop.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/desktop/", packaging, StringComparison.Ordinal);
        Assert.Contains("/opt/mfc/desktop/Mfc.Desktop", howto, StringComparison.Ordinal);
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
