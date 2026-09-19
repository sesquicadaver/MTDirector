using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-270: OPS-HOST-SYSTEMD-01 — systemd unit template for framework-dependent Controller publish layout.
/// </summary>
public sealed class OpsHostSystemd01ControllerUnitW7270LivingSpecTests
{
    [Fact]
    public void Ac1SystemdUnitTemplateMatchesPackageControllerLayoutAndDocs()
    {
        string root = RepoRoot();
        string unitPath = Path.Combine(root, "packaging/systemd/mfc-controller.service");
        string unit = File.ReadAllText(unitPath);
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.True(File.Exists(unitPath));
        Assert.Contains("[Unit]", unit, StringComparison.Ordinal);
        Assert.Contains("[Service]", unit, StringComparison.Ordinal);
        Assert.Contains("[Install]", unit, StringComparison.Ordinal);
        Assert.Contains("Description=MTDirector MikroTik Firewall Controller (Mfc.Controller)", unit, StringComparison.Ordinal);
        Assert.Contains("Type=simple", unit, StringComparison.Ordinal);
        Assert.Contains("WorkingDirectory=/opt/mfc/controller", unit, StringComparison.Ordinal);
        Assert.Contains("ExecStart=/opt/mfc/controller/Mfc.Controller", unit, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=-/etc/mfc/controller.env", unit, StringComparison.Ordinal);
        Assert.Contains("WantedBy=multi-user.target", unit, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", unit, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", unit, StringComparison.Ordinal);
        Assert.Contains("W7-22", unit, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", unit, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);

        Assert.Contains("packaging/systemd/mfc-controller.service", howto, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", installation, StringComparison.Ordinal);
        Assert.Contains("packaging/systemd/mfc-controller.service", packaging, StringComparison.Ordinal);
        Assert.Contains("packaging/systemd/mfc-controller.service", plan32, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-270 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("packaging/systemd/mfc-controller.service", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-270 | [#946](https://github.com/sesquicadaver/MTDirector/issues/946) | OPS-HOST-SYSTEMD-01 — systemd unit template for framework-dependent Controller | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-364 (#1135)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostSystemd01ControllerUnitW7270", testing, StringComparison.Ordinal);
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
