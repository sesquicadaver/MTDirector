using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-305: known-limitations / queue seed locked OPS-HOST-LOG-01 (W7-306) after PLAN-40 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7305LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostLog01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan40 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-40-controller-host-journald-syslog-identity.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));

        Assert.Contains("Intentional residual (W7-305 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-306", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-307", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-306**", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-315 (#1036)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-305", plan, StringComparison.Ordinal);
        Assert.Contains("W7-306", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-315 (#1036)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-305 (#1016) DONE", plan40, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-306", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-307", plan40, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-315 (#1036)", plan40, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("SyslogIdentifier=mfc-controller", unit, StringComparison.Ordinal);
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
