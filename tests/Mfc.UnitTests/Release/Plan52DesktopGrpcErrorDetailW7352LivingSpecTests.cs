using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-352: PLAN-52 inventory documents sole DESK-RPC-FAULT-01 rank
/// (14 ViewModel ErrorText sites ignore mfc-error-detail-bin)
/// and opens FAULT implement after seed.
/// </summary>
public sealed class Plan52DesktopGrpcErrorDetailW7352LivingSpecTests
{
    [Fact]
    public void Ac1Plan52InventoryDocumentsSoleDeskRpcFault01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan52 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-52-desktop-grpc-error-detail.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));

        Assert.Contains("PLAN-52 — Desktop gRPC ErrorDetail operator mapping after unary deadlines", plan52, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan52, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", plan52, StringComparison.Ordinal);
        Assert.Contains("7ee69220", plan52, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-354", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-355", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-353", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-352", plan52, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan52, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("14", plan52, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-374 (#1154)", plan52, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-352 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-354", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-353", limitations, StringComparison.Ordinal);
        Assert.Contains("7ee69220", limitations, StringComparison.Ordinal);
        Assert.Contains("14", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-352 | [#1111](https://github.com/sesquicadaver/MTDirector/issues/1111) | PLAN-52 — Inventory Desktop gRPC ErrorDetail operator mapping | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-353 | [#1112](https://github.com/sesquicadaver/MTDirector/issues/1112) | Seed first PLAN-52 atomic row after inventory → DESK-RPC-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-354 | [#1114](https://github.com/sesquicadaver/MTDirector/issues/1114) | DESK-RPC-FAULT-01 — Map Controller ErrorDetail trailer into operator ErrorText | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-355 | [#1115](https://github.com/sesquicadaver/MTDirector/issues/1115) | Seed next after DESK-RPC-FAULT-01 (PLAN-52 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-374 (#1154)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-353", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-354", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-52", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-52-desktop-grpc-error-detail.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-52-desktop-grpc-error-detail.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan52DesktopGrpcErrorDetailW7352", testing, StringComparison.Ordinal);

        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", helper, StringComparison.Ordinal);

        // FAULT-01 shipped: ViewModels use the shared trailer helper; unary deadline stays.
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        Assert.Contains("mfc-error-detail-bin", fault, StringComparison.Ordinal);
        Assert.Contains("correlation", fault, StringComparison.Ordinal);
        int sites = 0;
        string viewModels = Path.Combine(root, "src/Mfc.Desktop/ViewModels");
        foreach (string path in Directory.EnumerateFiles(viewModels, "*ViewModel.cs"))
        {
            string viewModel = File.ReadAllText(path);
            sites += Count(viewModel, "DesktopRpcFaultText.Format");
            Assert.DoesNotContain("ErrorText = ex.Status.Detail", viewModel, StringComparison.Ordinal);
        }

        Assert.Equal(14, sites);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + value.Length;
        }
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
