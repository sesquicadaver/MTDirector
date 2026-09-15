using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-272: OPS-HOST-WINSVC-01 — WinSW Windows Service template for framework-dependent Controller publish layout.
/// </summary>
public sealed class OpsHostWinsvc01ControllerUnitW7272LivingSpecTests
{
    [Fact]
    public void Ac1WinsvcTemplateMatchesPackageControllerLayoutAndDocs()
    {
        string root = RepoRoot();
        string winswPath = Path.Combine(root, "packaging/windows/mfc-controller.winsw.xml");
        string winsw = File.ReadAllText(winswPath);
        string systemdPath = Path.Combine(root, "packaging/systemd/mfc-controller.service");
        string systemd = File.ReadAllText(systemdPath);
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.True(File.Exists(winswPath));
        Assert.Contains("<service>", winsw, StringComparison.Ordinal);
        Assert.Contains("<id>mfc-controller</id>", winsw, StringComparison.Ordinal);
        Assert.Contains("<name>MTDirector MikroTik Firewall Controller (Mfc.Controller)</name>", winsw, StringComparison.Ordinal);
        Assert.Contains("<executable>%BASE%\\Mfc.Controller.exe</executable>", winsw, StringComparison.Ordinal);
        Assert.Contains("<workingdirectory>%BASE%</workingdirectory>", winsw, StringComparison.Ordinal);
        Assert.Contains("<startmode>Automatic</startmode>", winsw, StringComparison.Ordinal);
        Assert.Contains("<stoptimeout>60 sec</stoptimeout>", winsw, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", winsw, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", winsw, StringComparison.Ordinal);
        Assert.Contains("win-x64", winsw, StringComparison.Ordinal);
        Assert.Contains("W7-22", winsw, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", winsw, StringComparison.Ordinal);
        Assert.DoesNotContain(".msi", winsw, StringComparison.OrdinalIgnoreCase);

        // Do not regress systemd sibling
        Assert.True(File.Exists(systemdPath));
        Assert.Contains("ExecStart=/opt/mfc/controller/Mfc.Controller", systemd, StringComparison.Ordinal);
        Assert.Contains("EnvironmentFile=-/etc/mfc/controller.env", systemd, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);

        Assert.Contains("packaging/windows/mfc-controller.winsw.xml", howto, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.winsw.xml", installation, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-controller.winsw.xml", packaging, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-controller.winsw.xml", plan32, StringComparison.Ordinal);
        Assert.Contains("packaging/systemd/mfc-controller.service", packaging, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-272 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-controller.winsw.xml", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-272 | [#951](https://github.com/sesquicadaver/MTDirector/issues/951) | OPS-HOST-WINSVC-01 — Windows Service host template for framework-dependent Controller | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-273 (#952)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OpsHostWinsvc01ControllerUnitW7272", testing, StringComparison.Ordinal);
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
