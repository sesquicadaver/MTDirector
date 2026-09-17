using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-303: PLAN-39 COMPLETE; known-limitations / queue seed locked PLAN-40 inventory (W7-304)
/// and follow-up seed W7-305 after OPS-HOST-DOC-01.
/// </summary>
public sealed class ProductTrancheSeedW7303LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan40AfterPlan39Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan39 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-39-controller-host-operator-doc-packaging.md"));
        string plan40 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-40-controller-host-journald-syslog-identity.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string readme = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));

        Assert.Contains("Intentional residual (W7-303 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-39 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-40", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-304", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-305", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-LOG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-304**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-303 | [#1012](https://github.com/sesquicadaver/MTDirector/issues/1012) | Seed next after OPS-HOST-DOC-01 (PLAN-39 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-304 | [#1015](https://github.com/sesquicadaver/MTDirector/issues/1015) | PLAN-40 — Inventory Controller host journald/syslog identity (SyslogIdentifier) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-305 | [#1016](https://github.com/sesquicadaver/MTDirector/issues/1016) | Seed first PLAN-40 atomic row after inventory → OPS-HOST-LOG-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-347 (#1099)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-39 COMPLETE", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-303 (#1012) DONE", plan39, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-347 (#1099)", plan39, StringComparison.Ordinal);
        Assert.Contains("plan-40-controller-host-journald-syslog-identity.md", plan39, StringComparison.Ordinal);

        Assert.Contains("PLAN-40", plan, StringComparison.Ordinal);
        Assert.Contains("W7-304", plan, StringComparison.Ordinal);
        Assert.Contains("W7-303 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-347 (#1099)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-40-controller-host-journald-syslog-identity.md", plan, StringComparison.Ordinal);

        Assert.Contains("OPS-HOST-LOG-01", plan40, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-304", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-305", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-306", plan40, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-347 (#1099)", plan40, StringComparison.Ordinal);
        Assert.Contains("SyslogIdentifier", plan40, StringComparison.Ordinal);
        Assert.Contains("30bee1c0", plan40, StringComparison.Ordinal);

        Assert.Contains("Documentation=file:///usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);
        Assert.Contains("SyslogIdentifier=mfc-controller", unit, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", readme, StringComparison.Ordinal);
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
