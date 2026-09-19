using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-306: OPS-HOST-LOG-01 — SyslogIdentifier + journal stdout/stderr on mfc-controller.service.
/// </summary>
public sealed class OpsHostLog01ControllerJournaldIdentityW7306LivingSpecTests
{
    [Fact]
    public void Ac1UnitHasSyslogIdentifierAndJournalStdoutDocsLock()
    {
        string root = RepoRoot();
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan40 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-40-controller-host-journald-syslog-identity.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("SyslogIdentifier=mfc-controller", unit, StringComparison.Ordinal);
        Assert.Contains("StandardOutput=journal", unit, StringComparison.Ordinal);
        Assert.Contains("StandardError=journal", unit, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", unit, StringComparison.Ordinal);
        Assert.Contains("Documentation=file:///usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", unit, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", packageController, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("OPS-HOST-LOG-01", packaging, StringComparison.Ordinal);
        Assert.Contains("SyslogIdentifier=mfc-controller", packaging, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", howto, StringComparison.Ordinal);
        Assert.Contains("journalctl -t mfc-controller", howto, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", installation, StringComparison.Ordinal);
        Assert.Contains("journalctl -t mfc-controller", installation, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", plan40, StringComparison.Ordinal);
        Assert.Contains("SyslogIdentifier=mfc-controller", plan40, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-306 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-306 | [#1018](https://github.com/sesquicadaver/MTDirector/issues/1018) | OPS-HOST-LOG-01 — SyslogIdentifier + journal stdout/stderr on mfc-controller.service | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-376 (#1159)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostLog01ControllerJournaldIdentityW7306", testing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DryRunPublishTreeBundledUnitCarriesSyslogIdentifier()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/package-controller.sh");
        string outDir = Path.Combine(Path.GetTempPath(), "mfc-ops-log-" + Guid.NewGuid().ToString("N"));
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

            string bundledPath = Path.Combine(outDir, "controller", "mfc-controller.service");
            Assert.True(File.Exists(bundledPath));
            string bundled = File.ReadAllText(bundledPath);
            Assert.Contains("SyslogIdentifier=mfc-controller", bundled, StringComparison.Ordinal);
            Assert.Contains("StandardOutput=journal", bundled, StringComparison.Ordinal);
            Assert.Contains("StandardError=journal", bundled, StringComparison.Ordinal);
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
