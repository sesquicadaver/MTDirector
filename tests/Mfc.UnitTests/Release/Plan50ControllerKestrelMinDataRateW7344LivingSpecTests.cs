using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-344: PLAN-50 inventory documents sole CTRL-KESTREL-MINRATE-01 rank
/// (Kestrel MinRequestBodyDataRate / MinResponseDataRate → null/disabled)
/// and opens MINRATE implement after seed.
/// </summary>
public sealed class Plan50ControllerKestrelMinDataRateW7344LivingSpecTests
{
    [Fact]
    public void Ac1Plan50InventoryDocumentsSoleCtrlKestrelMinrate01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan50 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-50-controller-kestrel-min-data-rate.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("PLAN-50 — Controller Kestrel min request/response data-rate after HTTP/2 keepalive", plan50, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan50, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", plan50, StringComparison.Ordinal);
        Assert.Contains("be2ac7a8", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-346", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-345", plan50, StringComparison.Ordinal);
        Assert.Contains("W7-344", plan50, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan50, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MinRequestBodyDataRate", plan50, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate", plan50, StringComparison.Ordinal);
        Assert.Contains("null", plan50, StringComparison.Ordinal);
        Assert.Contains("240 B/s", plan50, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", plan50, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-344 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-MINRATE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-346", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-345", limitations, StringComparison.Ordinal);
        Assert.Contains("be2ac7a8", limitations, StringComparison.Ordinal);
        Assert.Contains("null", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-388 (#1183)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-345", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-346", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-50", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-50-controller-kestrel-min-data-rate.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-50-controller-kestrel-min-data-rate.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan50ControllerKestrelMinDataRateW7344", testing, StringComparison.Ordinal);

        // MINRATE-01 shipped: null/disabled min data rates (do not regress BODY/KEEPALIVE).
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay", program, StringComparison.Ordinal);
        Assert.Contains("MinResponseDataRate = null", program, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);
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
