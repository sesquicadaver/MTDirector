using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-251: known-limitations / queue seed locked DESK-CONN-HEALTH-01 (W7-252) after PLAN-29 inventory.
/// Historical: HEALTH-01 DONE; W7-253 seed DONE; NEXT advanced to W7-254 RECONNECT implement.
/// </summary>
public sealed class ProductTrancheSeedW7251LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskConnHealth01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));

        Assert.Contains("Intentional residual (W7-251 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-HEALTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-252", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-253", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-251 | [#908](https://github.com/sesquicadaver/MTDirector/issues/908) | Seed first PLAN-29 atomic row after inventory → DESK-CONN-HEALTH-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-252 | [#910](https://github.com/sesquicadaver/MTDirector/issues/910) | DESK-CONN-HEALTH-01 — Connected-state periodic gRPC health probe after Controller stop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-308 (#1023)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-251 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-252", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-HEALTH-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-308 (#1023)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-251 DONE", plan29, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-HEALTH-01", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-252", plan29, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-308 (#1023)", plan29, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-252**", limitations, StringComparison.Ordinal);
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
