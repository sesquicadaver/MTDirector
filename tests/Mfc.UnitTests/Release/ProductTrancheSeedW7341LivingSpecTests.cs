using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-341: known-limitations / queue seed locked CTRL-GRPC-KEEPALIVE-01 (W7-342) after PLAN-49 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7341LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlGrpcKeepalive01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan49 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-49-controller-grpc-http2-keepalive.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktopHandler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));

        Assert.Contains("Intentional residual (W7-341 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-342", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-343", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-342**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-341 | [#1088](https://github.com/sesquicadaver/MTDirector/issues/1088) | Seed first PLAN-49 atomic row after inventory → CTRL-GRPC-KEEPALIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-342 | [#1090](https://github.com/sesquicadaver/MTDirector/issues/1090) | CTRL-GRPC-KEEPALIVE-01 — Finite HTTP/2 keepalive for Controller+Desktop Watch streams | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-343 | [#1092](https://github.com/sesquicadaver/MTDirector/issues/1092) | Seed next after CTRL-GRPC-KEEPALIVE-01 (PLAN-49 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-385 (#1176)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-341", plan, StringComparison.Ordinal);
        Assert.Contains("W7-342", plan, StringComparison.Ordinal);
        Assert.Contains("W7-343", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-385 (#1176)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-341 (#1088) DONE", plan49, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-342", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-343", plan49, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-385 (#1176)", plan49, StringComparison.Ordinal);

        // KEEPALIVE-01 shipped after this seed.
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
        Assert.Contains("EnableMultipleHttp2Connections", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
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
