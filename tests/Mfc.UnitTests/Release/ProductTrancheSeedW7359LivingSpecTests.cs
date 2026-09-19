using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-359: PLAN-53 COMPLETE; known-limitations / queue seed locked PLAN-54 inventory (W7-360)
/// and follow-up seed W7-361 after CTRL-ERRDETAIL-LOG-01.
/// </summary>
public sealed class ProductTrancheSeedW7359LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan54AfterPlan53Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan53 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-53-controller-fault-correlation-log.md"));
        string plan54 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-54-desktop-connection-status-fault-text.md"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-359 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-53 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-54", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-360", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-361", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-360**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-359 | [#1123](https://github.com/sesquicadaver/MTDirector/issues/1123) | Seed next after CTRL-ERRDETAIL-LOG-01 (PLAN-53 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-360 | [#1127](https://github.com/sesquicadaver/MTDirector/issues/1127) | PLAN-54 — Inventory Desktop connection-status fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-361 | [#1128](https://github.com/sesquicadaver/MTDirector/issues/1128) | Seed first PLAN-54 atomic row after inventory → DESK-CONN-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-365 (#1136)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-53 COMPLETE", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-359 (#1123) DONE", plan53, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-365 (#1136)", plan53, StringComparison.Ordinal);
        Assert.Contains("plan-54-desktop-connection-status-fault-text.md", plan, StringComparison.Ordinal);

        Assert.Contains("PLAN-54", plan, StringComparison.Ordinal);
        Assert.Contains("W7-360", plan, StringComparison.Ordinal);
        Assert.Contains("W7-359 (#1123) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-365 (#1136)", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", plan54, StringComparison.Ordinal);
        Assert.Contains("Status.Detail", plan54, StringComparison.Ordinal);
        Assert.Contains("470696e9", plan54, StringComparison.Ordinal);
        Assert.Contains("W7-360", plan54, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-365 (#1136)", plan54, StringComparison.Ordinal);

        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
        Assert.DoesNotContain("ex.Status.Detail", connection, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code}", mapper, StringComparison.Ordinal);
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
