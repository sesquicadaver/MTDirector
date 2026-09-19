using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-356: PLAN-53 inventory documents sole CTRL-ERRDETAIL-LOG-01 rank
/// (43 ToRpcException callers omit correlationId; mapper has no logger)
/// and opens LOG implement after seed.
/// </summary>
public sealed class Plan53ControllerFaultCorrelationLogW7356LivingSpecTests
{
    [Fact]
    public void Ac1Plan53InventoryDocumentsSoleCtrlErrdetailLog01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan53 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-53-controller-fault-correlation-log.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));

        Assert.Contains("PLAN-53 — Controller fault-correlation logging after Desktop ErrorDetail mapping", plan53, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan53, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", plan53, StringComparison.Ordinal);
        Assert.Contains("dbbe0733", plan53, StringComparison.Ordinal);
        Assert.Contains("Guid.NewGuid", plan53, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan53, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("43", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-358", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-359", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-357", plan53, StringComparison.Ordinal);
        Assert.Contains("W7-356", plan53, StringComparison.Ordinal);
        Assert.Contains("traceId", plan53, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", plan53, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-356 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-358", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-357", limitations, StringComparison.Ordinal);
        Assert.Contains("dbbe0733", limitations, StringComparison.Ordinal);
        Assert.Contains("43", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-356 | [#1119](https://github.com/sesquicadaver/MTDirector/issues/1119) | PLAN-53 — Inventory Controller fault-correlation logging | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-362 (#1130)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-357", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-358", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-53", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-53-controller-fault-correlation-log.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-53-controller-fault-correlation-log.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan53ControllerFaultCorrelationLogW7356", testing, StringComparison.Ordinal);

        Assert.Contains("correlationId ?? Guid.NewGuid()", mapper, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Contains("ILogger", mapper, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}", mapper, StringComparison.Ordinal);
        Assert.Contains("correlation", fault, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", fault, StringComparison.Ordinal);

        int calls = 0;
        string controller = Path.Combine(root, "src/Mfc.Controller");
        foreach (string path in Directory.EnumerateFiles(controller, "*.cs", SearchOption.AllDirectories))
        {
            if (path.EndsWith("GrpcApplicationErrorMapper.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            calls += Count(source, "GrpcApplicationErrorMapper.ToRpcException");
            Assert.DoesNotContain("correlationId", source, StringComparison.Ordinal);
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
