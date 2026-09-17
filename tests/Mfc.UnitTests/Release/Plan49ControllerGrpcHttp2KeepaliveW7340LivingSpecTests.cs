using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-340: PLAN-49 inventory documents sole CTRL-GRPC-KEEPALIVE-01 rank
/// (Controller Kestrel Http2 + Desktop SocketsHttpHandler KeepAlivePing)
/// and opens KEEPALIVE implement after seed.
/// </summary>
public sealed class Plan49ControllerGrpcHttp2KeepaliveW7340LivingSpecTests
{
    [Fact]
    public void Ac1Plan49InventoryDocumentsSoleCtrlGrpcKeepalive01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan49 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-49-controller-grpc-http2-keepalive.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktopHandler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));

        Assert.Contains("PLAN-49 — Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits", plan49, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan49, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", plan49, StringComparison.Ordinal);
        Assert.Contains("be206f6c", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-342", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-341", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-340", plan49, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan49, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KeepAlivePingDelay = 60s", plan49, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingTimeout = 30s", plan49, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.MaxValue", plan49, StringComparison.Ordinal);
        Assert.Contains("InfiniteTimeSpan", plan49, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-341 (#1088)", plan49, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-340 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-GRPC-KEEPALIVE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-342", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-341", limitations, StringComparison.Ordinal);
        Assert.Contains("be206f6c", limitations, StringComparison.Ordinal);
        Assert.Contains("60s", limitations, StringComparison.Ordinal);
        Assert.Contains("30s", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-340 | [#1087](https://github.com/sesquicadaver/MTDirector/issues/1087) | PLAN-49 — Inventory Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-341 | [#1088](https://github.com/sesquicadaver/MTDirector/issues/1088) | Seed first PLAN-49 atomic row after inventory → CTRL-GRPC-KEEPALIVE-01 | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-342 | [#1090](https://github.com/sesquicadaver/MTDirector/issues/1090) | CTRL-GRPC-KEEPALIVE-01 — Finite HTTP/2 keepalive for Controller+Desktop Watch streams | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-341 (#1088)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-341", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-342", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-49", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-49-controller-grpc-http2-keepalive.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-49-controller-grpc-http2-keepalive.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan49ControllerGrpcHttp2KeepaliveW7340", testing, StringComparison.Ordinal);

        // Pre-implement: keepalive still absent (inventory does not ship code).
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepAlivePingDelay", program, StringComparison.Ordinal);
        Assert.Contains("EnableMultipleHttp2Connections", desktopHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepAlivePingDelay", desktopHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("KeepAlivePingTimeout", desktopHandler, StringComparison.Ordinal);
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
