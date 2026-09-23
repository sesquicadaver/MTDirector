using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-299: PLAN-38 COMPLETE; known-limitations / queue seed locked PLAN-39 inventory (W7-300)
/// and follow-up seed W7-301 after OPS-HOST-SYSUSERS-01.
/// </summary>
public sealed class ProductTrancheSeedW7299LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan39AfterPlan38Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan38 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-38-controller-host-sysusers-tmpfiles-packaging.md"));
        string plan39 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-39-controller-host-operator-doc-packaging.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string sysusers = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers"));

        Assert.Contains("Intentional residual (W7-299 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-38 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-39", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-300", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-301", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-300**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-299 | [#1004](https://github.com/sesquicadaver/MTDirector/issues/1004) | Seed next after OPS-HOST-SYSUSERS-01 (PLAN-38 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-300 | [#1007](https://github.com/sesquicadaver/MTDirector/issues/1007) | PLAN-39 — Inventory Controller host operator doc packaging (Documentation=/usr/share/doc/mfc) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-301 | [#1008](https://github.com/sesquicadaver/MTDirector/issues/1008) | Seed first PLAN-39 atomic row after inventory → OPS-HOST-DOC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-401 (#1206)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-38 COMPLETE", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-299 (#1004) DONE", plan38, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-401 (#1206)", plan38, StringComparison.Ordinal);
        Assert.Contains("plan-39-controller-host-operator-doc-packaging.md", plan38, StringComparison.Ordinal);

        Assert.Contains("PLAN-39", plan, StringComparison.Ordinal);
        Assert.Contains("W7-300", plan, StringComparison.Ordinal);
        Assert.Contains("W7-299 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-401 (#1206)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-39-controller-host-operator-doc-packaging.md", plan, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-DOC-01", plan39, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-300", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-301", plan39, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-401 (#1206)", plan39, StringComparison.Ordinal);
        Assert.Contains("Documentation=", plan39, StringComparison.Ordinal);
        Assert.Contains("7348ba5e", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-302", plan39, StringComparison.Ordinal);

        Assert.Contains("Documentation=file:///usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);
        Assert.Contains("u mfc", sysusers, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.tmpfiles")));
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
