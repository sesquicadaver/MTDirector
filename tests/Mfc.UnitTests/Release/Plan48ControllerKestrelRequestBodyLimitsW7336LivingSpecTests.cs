using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-336: PLAN-48 inventory documents sole CTRL-KESTREL-BODY-01 rank
/// (Kestrel MaxRequestBodySize aligned with GrpcTransportLimits.MaxMessageBytes)
/// and opens BODY implement after seed.
/// </summary>
public sealed class Plan48ControllerKestrelRequestBodyLimitsW7336LivingSpecTests
{
    [Fact]
    public void Ac1Plan48InventoryDocumentsSoleCtrlKestrelBody01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan48 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-48-controller-kestrel-request-body-limits.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string controllerConfig = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string contracts = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/GrpcTransportLimits.cs"));

        Assert.Contains("PLAN-48 — Controller Kestrel request-body / HTTP2 limits after gRPC message-size", plan48, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan48, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", plan48, StringComparison.Ordinal);
        Assert.Contains("319d35bd", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-338", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-337", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-336", plan48, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan48, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaxRequestBodySize", plan48, StringComparison.Ordinal);
        Assert.Contains("GrpcTransportLimits.MaxMessageBytes", plan48, StringComparison.Ordinal);
        Assert.Contains("256 MiB", plan48, StringComparison.Ordinal);
        Assert.Contains("268435456", plan48, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-341 (#1088)", plan48, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-336 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-338", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-337", limitations, StringComparison.Ordinal);
        Assert.Contains("319d35bd", limitations, StringComparison.Ordinal);

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
            "W7-341 | [#1088](https://github.com/sesquicadaver/MTDirector/issues/1088) | Seed first PLAN-49 atomic row after inventory → CTRL-GRPC-KEEPALIVE-01 | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-341 (#1088)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-337", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-338", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-48", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-48-controller-kestrel-request-body-limits.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-48-controller-kestrel-request-body-limits.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan48ControllerKestrelRequestBodyLimitsW7336", testing, StringComparison.Ordinal);

        // BODY-01 shipped: MaxRequestBodySize aligned with GrpcTransportLimits.
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MaxMessageBytes = 256 * 1024 * 1024", contracts, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
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
