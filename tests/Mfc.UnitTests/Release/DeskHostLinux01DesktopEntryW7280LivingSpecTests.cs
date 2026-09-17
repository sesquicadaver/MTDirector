using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-280: DESK-HOST-LINUX-01 — freedesktop .desktop template for framework-dependent Desktop publish layout.
/// </summary>
public sealed class DeskHostLinux01DesktopEntryW7280LivingSpecTests
{
    [Fact]
    public void Ac1DesktopEntryMatchesPackageDesktopLayoutAndDocs()
    {
        string root = RepoRoot();
        string desktopPath = Path.Combine(root, "packaging/linux/mfc-desktop.desktop");
        string desktop = File.ReadAllText(desktopPath);
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan34 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-34-desktop-operator-launch-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.True(File.Exists(desktopPath));
        Assert.Contains("[Desktop Entry]", desktop, StringComparison.Ordinal);
        Assert.Contains("Type=Application", desktop, StringComparison.Ordinal);
        Assert.Contains("Name=MTDirector Desktop", desktop, StringComparison.Ordinal);
        Assert.Contains("Exec=/opt/mfc/desktop/Mfc.Desktop", desktop, StringComparison.Ordinal);
        Assert.Contains("Path=/opt/mfc/desktop", desktop, StringComparison.Ordinal);
        Assert.Contains("TryExec=/opt/mfc/desktop/Mfc.Desktop", desktop, StringComparison.Ordinal);
        Assert.Contains("Terminal=false", desktop, StringComparison.Ordinal);
        Assert.Contains("package-desktop.sh", desktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", desktop, StringComparison.Ordinal);
        Assert.Contains("W7-22", desktop, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", desktop, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageDesktop, StringComparison.Ordinal);

        Assert.Contains("packaging/linux/mfc-desktop.desktop", howto, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop.desktop", installation, StringComparison.Ordinal);
        Assert.Contains("packaging/linux/mfc-desktop.desktop", packaging, StringComparison.Ordinal);
        Assert.Contains("packaging/linux/mfc-desktop.desktop", plan34, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-280 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-LINUX-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("packaging/linux/mfc-desktop.desktop", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-280 | [#966](https://github.com/sesquicadaver/MTDirector/issues/966) | DESK-HOST-LINUX-01 — freedesktop .desktop template for framework-dependent Desktop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-318 (#1042)", roadmap, StringComparison.Ordinal);
        Assert.Contains("DeskHostLinux01DesktopEntryW7280", testing, StringComparison.Ordinal);
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
