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
            "W7-335 | [#1076](https://github.com/sesquicadaver/MTDirector/issues/1076) | Seed next after CTRL-GRPC-MSGSIZE-01 (PLAN-47 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-336 | [#1079](https://github.com/sesquicadaver/MTDirector/issues/1079) | PLAN-48 — Inventory Controller Kestrel request-body / HTTP2 limits after gRPC message-size | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-337 | [#1080](https://github.com/sesquicadaver/MTDirector/issues/1080) | Seed first PLAN-48 atomic row after inventory → CTRL-KESTREL-BODY-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-338 | [#1082](https://github.com/sesquicadaver/MTDirector/issues/1082) | CTRL-KESTREL-BODY-01 — Align Kestrel MaxRequestBodySize with GrpcTransportLimits (256 MiB) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-339 | [#1084](https://github.com/sesquicadaver/MTDirector/issues/1084) | Seed next after CTRL-KESTREL-BODY-01 (PLAN-48 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-340 | [#1087](https://github.com/sesquicadaver/MTDirector/issues/1087) | PLAN-49 — Inventory Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-341 | [#1088](https://github.com/sesquicadaver/MTDirector/issues/1088) | Seed first PLAN-49 atomic row after inventory → CTRL-GRPC-KEEPALIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-412 (#1224)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-333", plan, StringComparison.Ordinal);
        Assert.Contains("W7-334", plan, StringComparison.Ordinal);
        Assert.Contains("W7-335", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-412 (#1224)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-333 (#1072) DONE", plan47, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-334", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-335", plan47, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-412 (#1224)", plan47, StringComparison.Ordinal);

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
