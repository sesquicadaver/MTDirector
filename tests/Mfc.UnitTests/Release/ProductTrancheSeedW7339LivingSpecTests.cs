using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-339: PLAN-48 COMPLETE; known-limitations / queue seed locked PLAN-49 inventory (W7-340)
/// and follow-up seed W7-341 after CTRL-KESTREL-BODY-01.
/// </summary>
public sealed class ProductTrancheSeedW7339LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan49AfterPlan48Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan48 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-48-controller-kestrel-request-body-limits.md"));
        string plan49 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-49-controller-grpc-http2-keepalive.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktopHandler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));

        Assert.Contains("Intentional residual (W7-339 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-48 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-49", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-340", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-341", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-340**", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-48 COMPLETE", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-339 (#1084) DONE", plan48, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan48, StringComparison.Ordinal);
        Assert.Contains("plan-49-controller-grpc-http2-keepalive.md", plan48, StringComparison.Ordinal);

        Assert.Contains("PLAN-49", plan, StringComparison.Ordinal);
        Assert.Contains("W7-340", plan, StringComparison.Ordinal);
        Assert.Contains("W7-339 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-49-controller-grpc-http2-keepalive.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", plan49, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-340", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-341", plan49, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan49, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePing", plan49, StringComparison.Ordinal);

        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("EnableMultipleHttp2Connections", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingTimeout = GrpcHttp2KeepAlive.PingTimeout", desktopHandler, StringComparison.Ordinal);
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
