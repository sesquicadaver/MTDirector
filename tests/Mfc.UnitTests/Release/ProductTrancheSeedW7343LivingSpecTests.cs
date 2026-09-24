using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-343: PLAN-49 COMPLETE; known-limitations / queue seed locked PLAN-50 inventory (W7-344)
/// and follow-up seed W7-345 after CTRL-GRPC-KEEPALIVE-01.
/// </summary>
public sealed class ProductTrancheSeedW7343LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan50AfterPlan49Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan49 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-49-controller-grpc-http2-keepalive.md"));
        string plan50 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-50-controller-kestrel-min-data-rate.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string desktopHandler = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcHttpHandlerFactory.cs"));

        Assert.Contains("Intentional residual (W7-343 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-49 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-50", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-344", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-345", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-346", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-344**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-343 | [#1092](https://github.com/sesquicadaver/MTDirector/issues/1092) | Seed next after CTRL-GRPC-KEEPALIVE-01 (PLAN-49 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-344 | [#1095](https://github.com/sesquicadaver/MTDirector/issues/1095) | PLAN-50 — Inventory Controller Kestrel min request/response data-rate for quiet Watch streams | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-345 | [#1096](https://github.com/sesquicadaver/MTDirector/issues/1096) | Seed first PLAN-50 atomic row after inventory → CTRL-KESTREL-MINRATE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-346 | [#1098](https://github.com/sesquicadaver/MTDirector/issues/1098) | CTRL-KESTREL-MINRATE-01 — Disable Kestrel MinRequest/ResponseDataRate for quiet Watch streams | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-49 COMPLETE", plan49, StringComparison.Ordinal);
        Assert.Contains("W7-343 (#1092) DONE", plan49, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan49, StringComparison.Ordinal);
        Assert.Contains("plan-50-controller-kestrel-min-data-rate.md", plan49, StringComparison.Ordinal);

        Assert.Contains("PLAN-50", plan, StringComparison.Ordinal);
        Assert.Contains("W7-344", plan, StringComparison.Ordinal);
        Assert.Contains("W7-343 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-50-controller-kestrel-min-data-rate.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-KESTREL-MINRATE-01", plan50, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-344", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-345", plan50, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", plan50, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate", plan50, StringComparison.Ordinal);
        Assert.Contains("240 B/s", plan50, StringComparison.Ordinal);

        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", desktopHandler, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);
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
