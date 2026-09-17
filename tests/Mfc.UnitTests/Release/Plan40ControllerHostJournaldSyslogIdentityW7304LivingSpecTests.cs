using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-304: PLAN-40 inventory documents sole OPS-HOST-LOG-01 rank (SyslogIdentifier + journal stdout/stderr)
/// and opens LOG-01 implement after seed.
/// </summary>
public sealed class Plan40ControllerHostJournaldSyslogIdentityW7304LivingSpecTests
{
    [Fact]
    public void Ac1Plan40InventoryDocumentsSoleLogRankAndSeedsOpsHostLog01()
    {
        string root = RepoRoot();
        string plan40 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-40-controller-host-journald-syslog-identity.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));

        Assert.Contains("PLAN-40 — Controller host journald/syslog identity", plan40, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan40, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", plan40, StringComparison.Ordinal);
        Assert.Contains("30bee1c0", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-306", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-305", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-304", plan40, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan40, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SyslogIdentifier=mfc-controller", plan40, StringComparison.Ordinal);
        Assert.Contains("StandardOutput=journal", plan40, StringComparison.Ordinal);
        Assert.Contains("StandardError=journal", plan40, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-339 (#1084)", plan40, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan40, StringComparison.Ordinal);
        Assert.Contains("journalctl", plan40, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-304 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-306", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-305", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-304 | [#1015](https://github.com/sesquicadaver/MTDirector/issues/1015) | PLAN-40 — Inventory Controller host journald/syslog identity (SyslogIdentifier) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-305 | [#1016](https://github.com/sesquicadaver/MTDirector/issues/1016) | Seed first PLAN-40 atomic row after inventory → OPS-HOST-LOG-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-306 | [#1018](https://github.com/sesquicadaver/MTDirector/issues/1018) | OPS-HOST-LOG-01 — SyslogIdentifier + journal stdout/stderr on mfc-controller.service | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-307 | [#1020](https://github.com/sesquicadaver/MTDirector/issues/1020) | Seed next after OPS-HOST-LOG-01 (PLAN-40 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-339 (#1084)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-305", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-306", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-40", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-40-controller-host-journald-syslog-identity.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-40-controller-host-journald-syslog-identity.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan40ControllerHostJournaldSyslogIdentityW7304", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("SyslogIdentifier=mfc-controller", unit, StringComparison.Ordinal);
        Assert.Contains("StandardOutput=journal", unit, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", installation, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.service")));
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
