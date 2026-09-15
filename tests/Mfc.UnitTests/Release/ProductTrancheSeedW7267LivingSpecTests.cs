using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-267: PLAN-31 COMPLETE; known-limitations / queue seed locked PLAN-32 inventory (W7-268)
/// and follow-up seed W7-269 after DESK-A11Y-RO-01.
/// Historical: inventory DONE; SYSTEMD/WINSVC opened; seed W7-269 DONE → OPS-HOST-SYSTEMD-01 as NEXT.
/// </summary>
public sealed class ProductTrancheSeedW7267LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan32AfterPlan31Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));
        string plan32 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-32-controller-host-process-packaging.md"));
        string howto = File.ReadAllText(Path.Combine(root, "docs/howto/build-and-run.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));

        Assert.Contains("Intentional residual (W7-267 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-31 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-32", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-268", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-269", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSTEMD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-268**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-267 | [#940](https://github.com/sesquicadaver/MTDirector/issues/940) | Seed next after DESK-A11Y-RO-01 (PLAN-31 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-268 | [#943](https://github.com/sesquicadaver/MTDirector/issues/943) | PLAN-32 — Inventory Controller host-process packaging templates (systemd / Windows Service) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-269 | [#944](https://github.com/sesquicadaver/MTDirector/issues/944) | Seed first PLAN-32 atomic row after inventory → OPS-HOST-SYSTEMD-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-276 (#958)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-31 COMPLETE", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-267 (#940) DONE", plan31, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-276 (#958)", plan31, StringComparison.Ordinal);

        Assert.Contains("PLAN-32", plan, StringComparison.Ordinal);
        Assert.Contains("W7-268", plan, StringComparison.Ordinal);
        Assert.Contains("W7-267 (#940) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-276 (#958)", plan, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-SYSTEMD-01", plan32, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-WINSVC-01", plan32, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan32, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-276 (#958)", plan32, StringComparison.Ordinal);
        Assert.Contains("a8834eb", plan32, StringComparison.Ordinal);

        Assert.Contains("systemd", howto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Service", howto, StringComparison.Ordinal);
        Assert.Contains("Mfc.Controller", installation, StringComparison.Ordinal);
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
