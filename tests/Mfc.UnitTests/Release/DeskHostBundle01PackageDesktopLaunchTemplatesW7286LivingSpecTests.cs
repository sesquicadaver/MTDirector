using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-286: DESK-HOST-BUNDLE-01 — package-desktop copies launch templates into OUT_DIR/desktop.
/// </summary>
public sealed class DeskHostBundle01PackageDesktopLaunchTemplatesW7286LivingSpecTests
{
    [Fact]
    public void Ac1PackageDesktopScriptCopiesLaunchTemplatesAndDocsLock()
    {
        string root = RepoRoot();
        string packageDesktop = File.ReadAllText(Path.Combine(root, "scripts/release/package-desktop.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan35 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-35-desktop-launch-template-publish-bundling.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("mfc_desktop_bundle_launch_templates", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop.desktop", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop-start-menu.ps1", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("DEST=\"$OUT_DIR/desktop\"", packageDesktop, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageDesktop, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", packageDesktop, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", packageDesktop, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("DESK-HOST-BUNDLE-01", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop.desktop", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-desktop-start-menu.ps1", packaging, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", howto, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", installation, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01", plan35, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-286 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-HOST-BUNDLE-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-286 | [#978](https://github.com/sesquicadaver/MTDirector/issues/978) | DESK-HOST-BUNDLE-01 — package-desktop copies launch templates into OUT_DIR/desktop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-385 (#1176)", roadmap, StringComparison.Ordinal);
        Assert.Contains("DeskHostBundle01PackageDesktopLaunchTemplatesW7286", testing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DryRunPublishTreeContainsBundledLaunchTemplates()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/package-desktop.sh");
        string outDir = Path.Combine(Path.GetTempPath(), "mfc-desk-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(script);
            psi.Environment["OUT_DIR"] = outDir;
            psi.Environment["MFC_RELEASE_DRY_RUN"] = "1";
            psi.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin";

            using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start package-desktop.sh");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60_000);
            Assert.True(proc.ExitCode == 0, $"dry-run failed: exit={proc.ExitCode}\nstdout={stdout}\nstderr={stderr}");

            Assert.True(File.Exists(Path.Combine(outDir, "desktop", "mfc-desktop.desktop")));
            Assert.True(File.Exists(Path.Combine(outDir, "desktop", "mfc-desktop-start-menu.ps1")));
            Assert.True(File.Exists(Path.Combine(outDir, "desktop", "Mfc.Desktop")));
            string desktop = File.ReadAllText(Path.Combine(outDir, "desktop", "mfc-desktop.desktop"));
            Assert.Contains("Exec=/opt/mfc/desktop/Mfc.Desktop", desktop, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
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
