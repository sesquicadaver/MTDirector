using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-353: known-limitations / queue seed locked DESK-RPC-FAULT-01 (W7-354) after PLAN-52 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7353LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskRpcFault01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan52 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-52-desktop-grpc-error-detail.md"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string viewModels = Path.Combine(root, "src/Mfc.Desktop/ViewModels");

        Assert.Contains("Intentional residual (W7-353 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-354", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-355", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-354**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-353 | [#1112](https://github.com/sesquicadaver/MTDirector/issues/1112) | Seed first PLAN-52 atomic row after inventory → DESK-RPC-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-354 | [#1114](https://github.com/sesquicadaver/MTDirector/issues/1114) | DESK-RPC-FAULT-01 — Map Controller ErrorDetail trailer into operator ErrorText | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-355 | [#1115](https://github.com/sesquicadaver/MTDirector/issues/1115) | Seed next after DESK-RPC-FAULT-01 (PLAN-52 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-354 (#1114)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-353", plan, StringComparison.Ordinal);
        Assert.Contains("W7-354", plan, StringComparison.Ordinal);
        Assert.Contains("W7-355", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-354 (#1114)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-353 (#1112) DONE", plan52, StringComparison.Ordinal);
        Assert.Contains("DESK-RPC-FAULT-01", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-354", plan52, StringComparison.Ordinal);
        Assert.Contains("W7-355", plan52, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-354 (#1114)", plan52, StringComparison.Ordinal);

        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", helper, StringComparison.Ordinal);
        int sites = 0;
        foreach (string path in Directory.EnumerateFiles(viewModels, "*ViewModel.cs"))
        {
            sites += Count(File.ReadAllText(path), "ErrorText = ex.Status.Detail");
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
