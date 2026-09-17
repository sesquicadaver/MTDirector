using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-269: known-limitations / queue seed locked OPS-HOST-SYSTEMD-01 (W7-270) after PLAN-32 inventory.
/// Historical: SYSTEMD-01 DONE; seed W7-271 DONE; NEXT = W7-272 WINSVC implement; W7-273 COMPLETE OPEN.
/// </summary>
public sealed class ProductTrancheSeedW7269LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostSystemd01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));

        Assert.Contains("Intentional residual (W7-269 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-270", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-271", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-270**", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-269 (#944) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-270", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-269 (#944) DONE", plan32, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-270", plan32, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-309 (#1024)", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-271", plan32, StringComparison.Ordinal);
        Assert.Contains("packaging/systemd/mfc-controller.service", plan32, StringComparison.Ordinal);
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
