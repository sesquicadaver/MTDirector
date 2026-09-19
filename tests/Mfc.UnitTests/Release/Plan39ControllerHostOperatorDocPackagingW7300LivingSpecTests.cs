using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-300: PLAN-39 inventory documents sole OPS-HOST-DOC-01 rank (Documentation= README + docs + bundle) and seeds DOC-01.
/// </summary>
public sealed class Plan39ControllerHostOperatorDocPackagingW7300LivingSpecTests
{
    [Fact]
    public void Ac1Plan39InventoryDocumentsSoleDocRankAndSeedsOpsHostDoc01()
    {
        string root = RepoRoot();
        string plan39 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-39-controller-host-operator-doc-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));

        Assert.Contains("PLAN-39 — Controller host operator doc packaging", plan39, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan39, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", plan39, StringComparison.Ordinal);
        Assert.Contains("7348ba5e", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-302", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-301", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-300", plan39, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan39, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("packaging/doc/mfc/README.md", plan39, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller", plan39, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-382 (#1170)", plan39, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", plan39, StringComparison.Ordinal);
        Assert.Contains("bundle", plan39, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Documentation=", plan39, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-300 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-302", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-301", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-300 | [#1007](https://github.com/sesquicadaver/MTDirector/issues/1007) | PLAN-39 — Inventory Controller host operator doc packaging (Documentation=/usr/share/doc/mfc) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-301 | [#1008](https://github.com/sesquicadaver/MTDirector/issues/1008) | Seed first PLAN-39 atomic row after inventory → OPS-HOST-DOC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-302 | [#1010](https://github.com/sesquicadaver/MTDirector/issues/1010) | OPS-HOST-DOC-01 — author packaging/doc/mfc/README.md + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-304 | [#1015](https://github.com/sesquicadaver/MTDirector/issues/1015) | PLAN-40 — Inventory Controller host journald/syslog identity (SyslogIdentifier) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-382 (#1170)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-301", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-302", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-39", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-39-controller-host-operator-doc-packaging.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-39-controller-host-operator-doc-packaging.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan39ControllerHostOperatorDocPackagingW7300", testing, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", packageController, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller/", packaging, StringComparison.Ordinal);
        Assert.Contains("Documentation=file:///usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", installation, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", installation, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.service")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/doc/mfc/README.md")));
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
