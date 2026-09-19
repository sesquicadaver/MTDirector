using System.Diagnostics;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-294: OPS-HOST-ENV-01 — mfc-controller.env.example + package-controller bundle.
/// </summary>
public sealed class OpsHostEnv01ControllerEnvSampleW7294LivingSpecTests
{
    [Fact]
    public void Ac1EnvExampleDocumentsMfcKeysAndDocsLock()
    {
        string root = RepoRoot();
        string envExample = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.env.example"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string configuration = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string plan37 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-37-controller-host-env-sample-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));

        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.env.example")));
        Assert.Contains("OPS-HOST-ENV-01", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__Database__ConnectionString=", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__Grpc__ListenAddress=", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__Security__RequireTls=", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__Security__MasterKeyProvider=", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__Security__TrustedCa__ProfilesDirectory=", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__RouterOs__Enabled=", envExample, StringComparison.Ordinal);
        Assert.Contains("MFC__RouterOs__WriteEnabled=", envExample, StringComparison.Ordinal);
        Assert.Contains("CHANGE_ME", envExample, StringComparison.Ordinal);
        Assert.DoesNotContain("MasterKeyBase64=A", envExample, StringComparison.Ordinal);

        Assert.Contains("mfc-controller.env.example", packageController, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc_controller_bundle_host_templates", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain("AppImage", packageController, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", packageController, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("OPS-HOST-ENV-01", packaging, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", packaging, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", howto, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", installation, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", configuration, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01", plan37, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", unit, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=-/etc/mfc/controller.env", unit, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-294 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-ENV-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-294 | [#994](https://github.com/sesquicadaver/MTDirector/issues/994) | OPS-HOST-ENV-01 — author mfc-controller.env.example + docs + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-389 (#1184)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostEnv01ControllerEnvSampleW7294", testing, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DryRunPublishTreeContainsBundledEnvExample()
    {
        string root = RepoRoot();
        string script = Path.Combine(root, "scripts/release/package-controller.sh");
        string outDir = Path.Combine(Path.GetTempPath(), "mfc-ops-env-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(File.Exists(Path.Combine(outDir, "controller", "Mfc.Controller")));
            string bundled = File.ReadAllText(Path.Combine(outDir, "controller", "mfc-controller.env.example"));
            Assert.Contains("MFC__Database__ConnectionString=", bundled, StringComparison.Ordinal);
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
