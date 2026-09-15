using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-268: PLAN-32 inventory documents ranked OPS-HOST-SYSTEMD/WINSVC rows and seeds OPS-HOST-SYSTEMD-01.</summary>
public sealed class Plan32ControllerHostProcessPackagingW7268LivingSpecTests
{
    [Fact]
    public void Ac1Plan32InventoryDocumentsRankedRowsAndSeedsOpsHostSystemd01()
    {
        string root = RepoRoot();
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));

        Assert.Contains("PLAN-32 — Controller host-process packaging templates (systemd / Windows Service)", plan32, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan32, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", plan32, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-270", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-271", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-272", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-273", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-269", plan32, StringComparison.Ordinal);
        Assert.Contains("a8834eb", plan32, StringComparison.Ordinal);
        Assert.Contains("packaging/systemd/mfc-controller.service", plan32, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-controller.winsw.xml", plan32, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", plan32, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller", plan32, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-283 (#972)", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-269 (#944) DONE", plan32, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-268 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-270", limitations, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-SYSTEMD-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-270", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-268 | [#943](https://github.com/sesquicadaver/MTDirector/issues/943) | PLAN-32 — Inventory Controller host-process packaging templates (systemd / Windows Service) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-269 | [#944](https://github.com/sesquicadaver/MTDirector/issues/944) | Seed first PLAN-32 atomic row after inventory → OPS-HOST-SYSTEMD-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-270 | [#946](https://github.com/sesquicadaver/MTDirector/issues/946) | OPS-HOST-SYSTEMD-01 — systemd unit template for framework-dependent Controller | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-271 | [#947](https://github.com/sesquicadaver/MTDirector/issues/947) | Seed next PLAN-32 row after OPS-HOST-SYSTEMD-01 → OPS-HOST-WINSVC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-272 | [#951](https://github.com/sesquicadaver/MTDirector/issues/951) | OPS-HOST-WINSVC-01 — Windows Service host template for framework-dependent Controller | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-273 | [#952](https://github.com/sesquicadaver/MTDirector/issues/952) | Seed next after OPS-HOST-WINSVC-01 (PLAN-32 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-283 (#972)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-269", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-270", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-32", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-32-controller-host-process-packaging.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-32-controller-host-process-packaging.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan32ControllerHostProcessPackagingW7268", testing, StringComparison.Ordinal);

        Assert.Contains("packaging/systemd/mfc-controller.service", howto, StringComparison.Ordinal);
        Assert.Contains("Start `Mfc.Controller`", installation, StringComparison.Ordinal);
        Assert.Contains("package-controller.sh", packaging, StringComparison.Ordinal);
        Assert.Contains("OUT_DIR/controller/", packaging, StringComparison.Ordinal);
        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("--self-contained false", packageController, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.service")));
        Assert.Contains(
            "ExecStart=/opt/mfc/controller/Mfc.Controller",
            File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service")),
            StringComparison.Ordinal);
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
