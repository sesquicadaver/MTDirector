using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-357: known-limitations / queue seed locked CTRL-ERRDETAIL-LOG-01 (W7-358) after PLAN-53 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7357LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlErrdetailLog01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan53 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-53-controller-fault-correlation-log.md"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));

        Assert.Contains("Intentional residual (W7-357 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-358", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-359", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-358**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-357 | [#1120](https://github.com/sesquicadaver/MTDirector/issues/1120) | Seed first PLAN-53 atomic row after inventory → CTRL-ERRDETAIL-LOG-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-358 | [#1122](https://github.com/sesquicadaver/MTDirector/issues/1122) | CTRL-ERRDETAIL-LOG-01 — Log fault code, status, correlation id, and retryable | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-359 | [#1123](https://github.com/sesquicadaver/MTDirector/issues/1123) | Seed next after CTRL-ERRDETAIL-LOG-01 (PLAN-53 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-357", plan, StringComparison.Ordinal);
        Assert.Contains("W7-358", plan, StringComparison.Ordinal);
        Assert.Contains("W7-359", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-357 (#1120) DONE", plan53, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-358", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-359", plan53, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", plan53, StringComparison.Ordinal);

        Assert.Contains("correlationId ?? Guid.NewGuid()", mapper, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code}", mapper, StringComparison.Ordinal);
        Assert.Contains("correlation", fault, StringComparison.Ordinal);

        int calls = 0;
        string controller = Path.Combine(root, "src/Mfc.Controller");
        foreach (string path in Directory.EnumerateFiles(controller, "*.cs", SearchOption.AllDirectories))
        {
            if (path.EndsWith("GrpcApplicationErrorMapper.cs", StringComparison.Ordinal))
            {
                continue;
            }

            calls += Count(File.ReadAllText(path), "GrpcApplicationErrorMapper.ToRpcException");
        }

        Assert.Equal(43, calls);
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
