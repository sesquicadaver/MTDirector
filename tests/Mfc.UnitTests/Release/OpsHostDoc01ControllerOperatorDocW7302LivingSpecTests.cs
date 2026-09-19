using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-302: OPS-HOST-DOC-01 — packaging/doc/mfc/README.md + package-controller bundle.
/// </summary>
public sealed class OpsHostDoc01ControllerOperatorDocW7302LivingSpecTests
{
    [Fact]
    public void Ac1OperatorDocMatchesDocumentationPathAndDocsLock()
    {
        string root = RepoRoot();
        string readme = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan39 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-39-controller-host-operator-doc-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));

        Assert.True(File.Exists(Path.Combine(root, "packaging/doc/mfc/README.md")));
        Assert.Contains("OPS-HOST-DOC-01", readme, StringComparison.Ordinal);
        Assert.Contains("/usr/share/doc/mfc/README.md", readme, StringComparison.Ordinal);
        Assert.Contains("Documentation=", readme, StringComparison.Ordinal);

        Assert.Contains("packaging/doc/mfc/README.md", packageController, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc_controller_bundle_host_templates", packageController, StringComparison.Ordinal);
        Assert.Contains("$dest/README.md", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", packageController, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("OPS-HOST-DOC-01", packaging, StringComparison.Ordinal);
        Assert.Contains("packaging/doc/mfc/README.md", packaging, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", howto, StringComparison.Ordinal);
        Assert.Contains("/usr/share/doc/mfc/README.md", installation, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", installation, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", plan39, StringComparison.Ordinal);
        Assert.Contains("Documentation=file:///usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);
        Assert.Contains("/usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-302 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-302 | [#1010](https://github.com/sesquicadaver/MTDirector/issues/1010) | OPS-HOST-DOC-01 — author packaging/doc/mfc/README.md + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostDoc01ControllerOperatorDocW7302", testing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DryRunPublishTreeContainsBundledOperatorReadme()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/package-controller.sh");
        string outDir = Path.Combine(Path.GetTempPath(), "mfc-ops-doc-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "mfc-controller.sysusers")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "README.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "Mfc.Controller")));
            string bundled = File.ReadAllText(Path.Combine(outDir, "controller", "README.md"));
            Assert.Contains("OPS-HOST-DOC-01", bundled, StringComparison.Ordinal);
            Assert.Contains("/usr/share/doc/mfc/README.md", bundled, StringComparison.Ordinal);
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
