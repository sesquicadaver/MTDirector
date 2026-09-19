using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-295: PLAN-37 COMPLETE; known-limitations / queue seed locked PLAN-38 inventory (W7-296)
/// and follow-up seed W7-297 after OPS-HOST-ENV-01.
/// </summary>
public sealed class ProductTrancheSeedW7295LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan38AfterPlan37Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan37 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-37-controller-host-env-sample-packaging.md"));
        string plan38 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-38-controller-host-sysusers-tmpfiles-packaging.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string envExample = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.env.example"));

        Assert.Contains("Intentional residual (W7-295 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-37 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-38", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-296", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-297", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-296**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-295 | [#996](https://github.com/sesquicadaver/MTDirector/issues/996) | Seed next after OPS-HOST-ENV-01 (PLAN-37 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-296 | [#999](https://github.com/sesquicadaver/MTDirector/issues/999) | PLAN-38 — Inventory Controller host sysusers/tmpfiles packaging (mfc user + /etc/mfc + /var/lib/mfc) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-297 | [#1000](https://github.com/sesquicadaver/MTDirector/issues/1000) | Seed first PLAN-38 atomic row after inventory → OPS-HOST-SYSUSERS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-37 COMPLETE", plan37, StringComparison.Ordinal);
        Assert.Contains("W7-295 (#996) DONE", plan37, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", plan37, StringComparison.Ordinal);
        Assert.Contains("plan-38-controller-host-sysusers-tmpfiles-packaging.md", plan37, StringComparison.Ordinal);

        Assert.Contains("PLAN-38", plan, StringComparison.Ordinal);
        Assert.Contains("W7-296", plan, StringComparison.Ordinal);
        Assert.Contains("W7-295 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-38-controller-host-sysusers-tmpfiles-packaging.md", plan, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-SYSUSERS-01", plan38, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-296", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-297", plan38, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-357 (#1120)", plan38, StringComparison.Ordinal);
        Assert.Contains("sysusers", plan38, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tmpfiles", plan38, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("7da14df5", plan38, StringComparison.Ordinal);

        Assert.Contains("User=mfc", unit, StringComparison.Ordinal);
        Assert.Contains("Group=mfc", unit, StringComparison.Ordinal);
        Assert.Contains("/var/lib/mfc/trusted-ca", envExample, StringComparison.Ordinal);
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
