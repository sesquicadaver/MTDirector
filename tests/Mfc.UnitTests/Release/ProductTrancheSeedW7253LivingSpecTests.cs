using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-253: seed locked DESK-CONN-RECONNECT-01 (W7-254); opens PLAN-29 COMPLETE follow-up (W7-255).
/// Historical: RECONNECT-01 DONE; NEXT advanced to W7-255.
/// </summary>
public sealed class ProductTrancheSeedW7253LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskConnReconnect01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));

        Assert.Contains("Intentional residual (W7-253 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-RECONNECT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-254", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-255", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-254**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-253 | [#911](https://github.com/sesquicadaver/MTDirector/issues/911) | Seed next PLAN-29 row after DESK-CONN-HEALTH-01 → DESK-CONN-RECONNECT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-254 | [#915](https://github.com/sesquicadaver/MTDirector/issues/915) | DESK-CONN-RECONNECT-01 — Bounded reconnect after health-fail drop + shell StatusText/LastError sync | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-255 | [#916](https://github.com/sesquicadaver/MTDirector/issues/916) | Seed next after DESK-CONN-RECONNECT-01 (PLAN-29 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-358 (#1122)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-253 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-254", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-RECONNECT-01", plan, StringComparison.Ordinal);
        Assert.Contains("W7-255", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-358 (#1122)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-253 (#911) DONE", plan29, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-RECONNECT-01", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-254 (#915) DONE", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-255 (#916)", plan29, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-358 (#1122)", plan29, StringComparison.Ordinal);
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
