using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-364: PLAN-55 inventory documents sole SNAP-FAULT-CORR-01 rank
/// (4 capture-progress CorrelationIds are Guid.NewGuid; two ToRpcException throws omit that id)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan55CaptureProgressFaultCorrelationW7364LivingSpecTests
{
    [Fact]
    public void Ac1Plan55InventoryDocumentsSoleSnapFaultCorr01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan55 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-55-capture-progress-fault-correlation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));

        Assert.Contains("PLAN-55 — Capture progress fault correlation after connection-status fault text", plan55, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan55, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", plan55, StringComparison.Ordinal);
        Assert.Contains("ded885b8", plan55, StringComparison.Ordinal);
        Assert.Contains("d848a58c", plan55, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan55, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SanitizedDetail", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-366", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-367", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-365", plan55, StringComparison.Ordinal);
        Assert.Contains("W7-364", plan55, StringComparison.Ordinal);
        Assert.Contains("FormatCaptureProgress", plan55, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", plan55, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-364 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-366", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-365", limitations, StringComparison.Ordinal);
        Assert.Contains("ded885b8", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-364 | [#1135](https://github.com/sesquicadaver/MTDirector/issues/1135) | PLAN-55 — Inventory capture progress fault correlation | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-365 | [#1136](https://github.com/sesquicadaver/MTDirector/issues/1136) | Seed first PLAN-55 atomic row after inventory → SNAP-FAULT-CORR-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-366 | [#1138](https://github.com/sesquicadaver/MTDirector/issues/1138) | SNAP-FAULT-CORR-01 — Share capture progress correlation id with RPC fault | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-367 | [#1139](https://github.com/sesquicadaver/MTDirector/issues/1139) | Seed next after SNAP-FAULT-CORR-01 (PLAN-55 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-414 (#1228)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-365", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-366", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-55", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-55-capture-progress-fault-correlation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-55-capture-progress-fault-correlation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan55CaptureProgressFaultCorrelationW7364", testing, StringComparison.Ordinal);

        Assert.Equal(0, Count(snapshot, "CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid())"));
        Assert.Equal(2, Count(snapshot, "ToRpcException(result.Error!, sharedId)"));
        Assert.Contains("NewCaptureFailureDetail", snapshot, StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("CaptureProgressText = $\"Failed: {fault}\"", viewer, StringComparison.Ordinal);
        Assert.Contains("public static RpcException ToRpcException(ApplicationError error, Guid? correlationId = null)", mapper, StringComparison.Ordinal);
        Assert.Contains("gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}", mapper, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Equal(4, Count(connection, "DesktopRpcFaultText.Format(ex)"));
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
