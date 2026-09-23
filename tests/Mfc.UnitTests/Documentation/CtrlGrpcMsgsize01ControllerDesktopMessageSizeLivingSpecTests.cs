using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-GRPC-MSGSIZE-01: Controller + Desktop MaxReceive/SendMessageSize via shared
/// GrpcTransportLimits.MaxMessageBytes (256 MiB), aligned with RawSnapshotLimits.
/// </summary>
public sealed class CtrlGrpcMsgsize01ControllerDesktopMessageSizeLivingSpecTests
{
    [Fact]
    public void Ac1SharedConstantAndBothSidesConfigureFiniteMessageSize()
    {
        string root = RepoRoot();
        string contracts = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/GrpcTransportLimits.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktop = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string rawLimits = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Snapshot/RawSnapshotModels.cs"));

        Assert.Contains("CTRL-GRPC-MSGSIZE-01", contracts, StringComparison.Ordinal);
        Assert.Contains("MaxMessageBytes = 256 * 1024 * 1024", contracts, StringComparison.Ordinal);
        Assert.Contains("268435456", contracts, StringComparison.Ordinal);
        Assert.Equal(268435456, Mfc.Contracts.GrpcTransportLimits.MaxMessageBytes);
        Assert.Contains("MaxSnapshotBytes = 256L * 1024L * 1024L", rawLimits, StringComparison.Ordinal);

        Assert.Contains("CTRL-GRPC-MSGSIZE-01", program, StringComparison.Ordinal);
        Assert.Contains("AddGrpc(options =>", program, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MaxSendMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxReceiveMessageSize = null", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxSendMessageSize = null", program, StringComparison.Ordinal);

        Assert.Contains("CTRL-GRPC-MSGSIZE-01", desktop, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", desktop, StringComparison.Ordinal);
        Assert.Contains("MaxSendMessageSize = GrpcTransportLimits.MaxMessageBytes", desktop, StringComparison.Ordinal);

        // Do not regress health / metrics / tracing / resource.
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("AddGrpcHealthChecks", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsAndQueueLockMessageSizeLimits()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string controllerConfig = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan47 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-47-controller-grpc-message-size-limits.md"));

        Assert.Contains("CTRL-GRPC-MSGSIZE-01", installation, StringComparison.Ordinal);
        Assert.Contains("GrpcTransportLimits.MaxMessageBytes", installation, StringComparison.Ordinal);
        Assert.Contains("256 MiB", installation, StringComparison.Ordinal);
        Assert.Contains("268435456", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);

        Assert.Contains("CTRL-GRPC-MSGSIZE-01", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("GrpcTransportLimits.MaxMessageBytes", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("268435456", controllerConfig, StringComparison.Ordinal);
        Assert.Contains("never unlimited", controllerConfig, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("CTRL-GRPC-MSGSIZE-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlGrpcMsgsize01ControllerDesktopMessageSizeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-334 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-334 | [#1074](https://github.com/sesquicadaver/MTDirector/issues/1074) | CTRL-GRPC-MSGSIZE-01 — Align Controller+Desktop gRPC MaxReceive/SendMessageSize with snapshot bounds | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-395 (#1197)", roadmap, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-MSGSIZE-01", plan47, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-334)", plan47, StringComparison.Ordinal);
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
