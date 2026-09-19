using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-345: known-limitations / queue seed locked CTRL-KESTREL-MINRATE-01 (W7-346) after PLAN-50 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7345LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlKestrelMinrate01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan50 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-50-controller-kestrel-min-data-rate.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Intentional residual (W7-345 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-346", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-347", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-346**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-345 | [#1096](https://github.com/sesquicadaver/MTDirector/issues/1096) | Seed first PLAN-50 atomic row after inventory → CTRL-KESTREL-MINRATE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-346 | [#1098](https://github.com/sesquicadaver/MTDirector/issues/1098) | CTRL-KESTREL-MINRATE-01 — Disable Kestrel MinRequest/ResponseDataRate for quiet Watch streams | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-347 | [#1099](https://github.com/sesquicadaver/MTDirector/issues/1099) | Seed next after CTRL-KESTREL-MINRATE-01 (PLAN-50 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-359 (#1123)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-345", plan, StringComparison.Ordinal);
        Assert.Contains("W7-346", plan, StringComparison.Ordinal);
        Assert.Contains("W7-347", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-359 (#1123)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-345 (#1096) DONE", plan50, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-346", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-347", plan50, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-359 (#1123)", plan50, StringComparison.Ordinal);

        // MINRATE-01 shipped after this seed.
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
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
