using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-271: seed locked OPS-HOST-WINSVC-01 (W7-272); opens PLAN-32 COMPLETE follow-up (W7-273).
/// Historical: WINSVC-01 DONE; NEXT = W7-273 PLAN-32 COMPLETE seed.
/// </summary>
public sealed class ProductTrancheSeedW7271LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostWinsvc01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));

        Assert.Contains("Intentional residual (W7-271 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-272", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-273", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-272**", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-342 (#1090)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-271 (#947) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-272", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-273", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-342 (#1090)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-271 (#947) DONE", plan32, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-272 (#951)", plan32, StringComparison.Ordinal);
        Assert.Contains("W7-273 (#952)", plan32, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-342 (#1090)", plan32, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/mfc-controller.winsw.xml", plan32, StringComparison.Ordinal);
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
