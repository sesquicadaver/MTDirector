using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-296: PLAN-38 inventory documents sole OPS-HOST-SYSUSERS-01 rank (sysusers + tmpfiles + docs + bundle) and seeds SYSUSERS-01.
/// </summary>
public sealed class Plan38ControllerHostSysusersTmpfilesPackagingW7296LivingSpecTests
{
    [Fact]
    public void Ac1Plan38InventoryDocumentsSoleSysusersRankAndSeedsOpsHostSysusers01()
    {
        string root = RepoRoot();
        string plan38 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-38-controller-host-sysusers-tmpfiles-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string envExample = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.env.example"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));

        Assert.Contains("PLAN-38 — Controller host sysusers/tmpfiles packaging", plan38, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan38, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", plan38, StringComparison.Ordinal);
        Assert.Contains("7da14df5", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-298", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-297", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-296", plan38, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan38, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mfc-controller.sysusers", plan38, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.tmpfiles", plan38, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller", plan38, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", plan38, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan38, StringComparison.Ordinal);
        Assert.Contains("bundle", plan38, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Intentional residual (W7-296 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-298", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-297", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-296 | [#999](https://github.com/sesquicadaver/MTDirector/issues/999) | PLAN-38 — Inventory Controller host sysusers/tmpfiles packaging (mfc user + /etc/mfc + /var/lib/mfc) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-297 | [#1000](https://github.com/sesquicadaver/MTDirector/issues/1000) | Seed first PLAN-38 atomic row after inventory → OPS-HOST-SYSUSERS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-298 | [#1002](https://github.com/sesquicadaver/MTDirector/issues/1002) | OPS-HOST-SYSUSERS-01 — author sysusers.d/tmpfiles.d + docs + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-421 (#1239)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-297", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-298", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-38", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-38-controller-host-sysusers-tmpfiles-packaging.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-38-controller-host-sysusers-tmpfiles-packaging.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan38ControllerHostSysusersTmpfilesPackagingW7296", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.tmpfiles", packageController, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller/", packaging, StringComparison.Ordinal);
        Assert.Contains("User=mfc", unit, StringComparison.Ordinal);
        Assert.Contains("Group=mfc", unit, StringComparison.Ordinal);
        Assert.Contains("/var/lib/mfc/trusted-ca", envExample, StringComparison.Ordinal);
        Assert.Contains("systemd-sysusers", installation, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", installation, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.service")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.env.example")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.tmpfiles")));
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
