using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-290: OPS-HOST-BUNDLE-01 — package-controller copies host templates into OUT_DIR/controller.
/// </summary>
public sealed class OpsHostBundle01PackageControllerHostTemplatesW7290LivingSpecTests
{
    [Fact]
    public void Ac1PackageControllerScriptCopiesHostTemplatesAndDocsLock()
    {
        string root = RepoRoot();
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan36 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-36-controller-host-template-publish-bundling.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("mfc_controller_bundle_host_templates", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.winsw.xml", packageController, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", packageController, StringComparison.Ordinal);
        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", packageController, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("OPS-HOST-BUNDLE-01", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.winsw.xml", packaging, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", howto, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", installation, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01", plan36, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-290 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-BUNDLE-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-290 | [#986](https://github.com/sesquicadaver/MTDirector/issues/986) | OPS-HOST-BUNDLE-01 — package-controller copies systemd/WinSW into OUT_DIR/controller | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostBundle01PackageControllerHostTemplatesW7290", testing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DryRunPublishTreeContainsBundledHostTemplates()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/package-controller.sh");
        string outDir = Path.Combine(Path.GetTempPath(), "mfc-ops-bundle-" + Guid.NewGuid().ToString("N"));
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

            using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start package-controller.sh");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60_000);
            Assert.True(proc.ExitCode == 0, $"dry-run failed: exit={proc.ExitCode}\nstdout={stdout}\nstderr={stderr}");

            Assert.True(File.Exists(Path.Combine(outDir, "controller", "mfc-controller.service")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "mfc-controller.winsw.xml")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "Mfc.Controller")));
            string unit = File.ReadAllText(Path.Combine(outDir, "controller", "mfc-controller.service"));
            Assert.Contains("/opt/mfc/controller/Mfc.Controller", unit, StringComparison.Ordinal);
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
