using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-282: DESK-HOST-WIN-01 — Windows Start Menu shortcut sketch for framework-dependent Desktop publish layout.
/// </summary>
public sealed class DeskHostWin01StartMenuSketchW7282LivingSpecTests
{
    [Fact]
    public void Ac1StartMenuSketchMatchesPackageDesktopLayoutAndDocs()
    {
        string root = RepoRoot();
        string scriptPath = Path.Combine(root, "packaging/windows/mfc-desktop-start-menu.ps1");
        string script = File.ReadAllText(scriptPath);
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string linuxDesktop = File.ReadAllText(Path.Combine(root, "packaging/linux/mfc-desktop.desktop"));

        Assert.True(File.Exists(scriptPath));
        Assert.Contains("DESK-HOST-WIN-01", script, StringComparison.Ordinal);
        Assert.Contains("Mfc.Desktop.exe", script, StringComparison.Ordinal);
        Assert.Contains("package-desktop.sh", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", script, StringComparison.Ordinal);
        Assert.Contains("W7-22", script, StringComparison.Ordinal);
        Assert.Contains("CreateShortcut", script, StringComparison.Ordinal);
        Assert.Contains("InstallRoot", script, StringComparison.Ordinal);
        Assert.Contains(@"C:\mfc\desktop", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageDesktop, StringComparison.Ordinal);

        Assert.Contains("packaging/windows/mfc-desktop-start-menu.ps1", howto, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop-start-menu.ps1", installation, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-desktop-start-menu.ps1", packaging, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-desktop-start-menu.ps1", plan34, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-282 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-WIN-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-desktop-start-menu.ps1", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-282 | [#971](https://github.com/sesquicadaver/MTDirector/issues/971) | DESK-HOST-WIN-01 — Windows Start Menu shortcut sketch for framework-dependent Desktop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-330 (#1066)", roadmap, StringComparison.Ordinal);
        Assert.Contains("DeskHostWin01StartMenuSketchW7282", testing, StringComparison.Ordinal);

        // Do not regress LINUX template
        Assert.Contains("Exec=/opt/mfc/desktop/Mfc.Desktop", linuxDesktop, StringComparison.Ordinal);
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
