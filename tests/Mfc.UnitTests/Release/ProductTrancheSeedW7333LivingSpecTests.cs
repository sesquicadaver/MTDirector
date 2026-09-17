using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-333: known-limitations / queue seed locked CTRL-GRPC-MSGSIZE-01 (W7-334) after PLAN-47 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7333LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlGrpcMsgsize01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan47 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-47-controller-grpc-message-size-limits.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktop = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));

        Assert.Contains("Intentional residual (W7-333 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-334", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-335", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-334**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-333 | [#1072](https://github.com/sesquicadaver/MTDirector/issues/1072) | Seed first PLAN-47 atomic row after inventory → CTRL-GRPC-MSGSIZE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-334 | [#1074](https://github.com/sesquicadaver/MTDirector/issues/1074) | CTRL-GRPC-MSGSIZE-01 — Align Controller+Desktop gRPC MaxReceive/SendMessageSize with snapshot bounds | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-335 | [#1076](https://github.com/sesquicadaver/MTDirector/issues/1076) | Seed next after CTRL-GRPC-MSGSIZE-01 (PLAN-47 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-335 (#1076)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-333", plan, StringComparison.Ordinal);
        Assert.Contains("W7-334", plan, StringComparison.Ordinal);
        Assert.Contains("W7-335", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-335 (#1076)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-333 (#1072) DONE", plan47, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-334", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-335", plan47, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-335 (#1076)", plan47, StringComparison.Ordinal);

        // Seed does not implement message-size.
        Assert.Contains("AddGrpc(options =>", program, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("GrpcChannel.ForAddress", desktop, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", desktop, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
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
