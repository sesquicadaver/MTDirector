using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-391: PLAN-61 COMPLETE. Operator fault-correlation wave PLAN-52…61 is CLOSED.
/// Freeze W7-392 closed that wave; PLAN-62 may follow from audit TOR only.
/// </summary>
public sealed class ProductTrancheSeedW7391LivingSpecTests
{
    [Fact]
    public void Ac1Plan61CompleteFreezesCorrelationWave()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));

        Assert.Contains("Intentional residual (W7-391 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-61 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-52…61 CLOSED", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-392", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-62", limitations, StringComparison.Ordinal);
        Assert.Contains("audit/TOR", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-391 | [#1187](https://github.com/sesquicadaver/MTDirector/issues/1187) | Seed next after DESK-CONN-DISC-01 (PLAN-61 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-392 | [#1191](https://github.com/sesquicadaver/MTDirector/issues/1191) | Freeze — no further correlation-id / fault-text plans without a pre-existing TOR | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-61 COMPLETE", plan61, StringComparison.Ordinal);
        Assert.Contains("PLAN-52…61 CLOSED", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-391 (#1187) DONE", plan61, StringComparison.Ordinal);
        Assert.Contains("W7-392", plan61, StringComparison.Ordinal);
        Assert.Contains("Not seeded", plan61, StringComparison.Ordinal);
        Assert.Contains("DESK-*-FAULT", plan61, StringComparison.Ordinal);
        Assert.Contains("SNAP-*-CORR", plan61, StringComparison.Ordinal);
        Assert.Contains("ErrorCode", plan61, StringComparison.Ordinal);
        Assert.Contains("a11y", plan61, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Type=notify", plan61, StringComparison.Ordinal);
        Assert.Contains("PLAN-62", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", plan61, StringComparison.Ordinal);

        Assert.Contains("PLAN-52…61 CLOSED", plan, StringComparison.Ordinal);
        Assert.Contains("W7-392", plan, StringComparison.Ordinal);
        Assert.Contains("PLAN-62", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-405 (#1212)", plan, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "docs/planning/plan-62-desktop-correlation-id.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md")));

        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.Disconnected, DesktopRpcFaultText.Format(ex))"));
        Assert.Equal(0, Count(connection, "SetState(ControllerConnectionState.Disconnected, ex.Message)"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + value.Length;
        }
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
