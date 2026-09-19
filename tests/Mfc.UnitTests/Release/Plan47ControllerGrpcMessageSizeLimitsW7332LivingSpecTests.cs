using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-332: PLAN-47 inventory documents sole CTRL-GRPC-MSGSIZE-01 rank
/// (Controller+Desktop MaxReceive/SendMessageSize aligned with snapshot bounds)
/// and opens MSGSIZE implement after seed.
/// </summary>
public sealed class Plan47ControllerGrpcMessageSizeLimitsW7332LivingSpecTests
{
    [Fact]
    public void Ac1Plan47InventoryDocumentsSoleCtrlGrpcMsgsize01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan47 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-47-controller-grpc-message-size-limits.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string controllerConfig = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktop = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string rawLimits = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Snapshot/RawSnapshotModels.cs"));

        Assert.Contains("PLAN-47 — Controller gRPC message-size / transport limits after OTel resource identity", plan47, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan47, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", plan47, StringComparison.Ordinal);
        Assert.Contains("d107b57d", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-334", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-333", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-332", plan47, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan47, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaxReceiveMessageSize", plan47, StringComparison.Ordinal);
        Assert.Contains("MaxSendMessageSize", plan47, StringComparison.Ordinal);
        Assert.Contains("RawSnapshotLimits.MaxSnapshotBytes", plan47, StringComparison.Ordinal);
        Assert.Contains("256 MiB", plan47, StringComparison.Ordinal);
        Assert.Contains("268435456", plan47, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-378 (#1162)", plan47, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-332 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-334", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-333", limitations, StringComparison.Ordinal);
        Assert.Contains("d107b57d", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-332 | [#1071](https://github.com/sesquicadaver/MTDirector/issues/1071) | PLAN-47 — Inventory Controller gRPC message-size / transport limits after OTel resource identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-378 (#1162)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-333", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-334", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-47", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-47-controller-grpc-message-size-limits.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-47-controller-grpc-message-size-limits.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan47ControllerGrpcMessageSizeLimitsW7332", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement message-size yet).
        Assert.Contains("AddGrpc(options =>", program, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("GrpcChannel.ForAddress", desktop, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", desktop, StringComparison.Ordinal);
        Assert.Contains("MaxSnapshotBytes = 256L * 1024L * 1024L", rawLimits, StringComparison.Ordinal);
        Assert.Contains("controller-configuration.md", installation, StringComparison.Ordinal);
        Assert.Contains("Grpc:ListenAddress", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "src/Mfc.Controller/Program.cs")));
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
