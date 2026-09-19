using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-298: OPS-HOST-SYSUSERS-01 — mfc-controller.sysusers/tmpfiles + package-controller bundle.
/// </summary>
public sealed class OpsHostSysusers01ControllerSysusersTmpfilesW7298LivingSpecTests
{
    [Fact]
    public void Ac1SysusersTmpfilesMatchUnitPathsAndDocsLock()
    {
        string root = RepoRoot();
        string sysusers = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers"));
        string tmpfiles = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.tmpfiles"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan38 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-38-controller-host-sysusers-tmpfiles-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string envExample = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.env.example"));

        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.tmpfiles")));
        Assert.Contains("OPS-HOST-SYSUSERS-01", sysusers, StringComparison.Ordinal);
        Assert.Contains("u mfc", sysusers, StringComparison.Ordinal);
        Assert.Contains("/var/lib/mfc", sysusers, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", tmpfiles, StringComparison.Ordinal);
        Assert.Contains("/etc/mfc", tmpfiles, StringComparison.Ordinal);
        Assert.Contains("/var/lib/mfc", tmpfiles, StringComparison.Ordinal);
        Assert.Contains("/var/lib/mfc/trusted-ca", tmpfiles, StringComparison.Ordinal);
        Assert.Contains("/opt/mfc/controller", tmpfiles, StringComparison.Ordinal);
        Assert.Contains("mfc  mfc", tmpfiles, StringComparison.Ordinal);

        Assert.Contains("mfc-controller.sysusers", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.tmpfiles", packageController, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc_controller_bundle_host_templates", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", packageController, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("OPS-HOST-SYSUSERS-01", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.tmpfiles", packaging, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", howto, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", installation, StringComparison.Ordinal);
        Assert.Contains("systemd-sysusers", installation, StringComparison.Ordinal);
        Assert.Contains("systemd-tmpfiles", installation, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", plan38, StringComparison.Ordinal);
        Assert.Contains("User=mfc", unit, StringComparison.Ordinal);
        Assert.Contains("Group=mfc", unit, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", unit, StringComparison.Ordinal);
        Assert.Contains("/var/lib/mfc/trusted-ca", envExample, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-298 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-298 | [#1002](https://github.com/sesquicadaver/MTDirector/issues/1002) | OPS-HOST-SYSUSERS-01 — author sysusers.d/tmpfiles.d + docs + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-364 (#1135)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostSysusers01ControllerSysusersTmpfilesW7298", testing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DryRunPublishTreeContainsBundledSysusersTmpfiles()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/package-controller.sh");
        string outDir = Path.Combine(Path.GetTempPath(), "mfc-ops-sysusers-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "mfc-controller.env.example")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "mfc-controller.sysusers")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "mfc-controller.tmpfiles")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "Mfc.Controller")));
            string bundledSysusers = File.ReadAllText(Path.Combine(outDir, "controller", "mfc-controller.sysusers"));
            string bundledTmpfiles = File.ReadAllText(Path.Combine(outDir, "controller", "mfc-controller.tmpfiles"));
            Assert.Contains("u mfc", bundledSysusers, StringComparison.Ordinal);
            Assert.Contains("/var/lib/mfc/trusted-ca", bundledTmpfiles, StringComparison.Ordinal);
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
