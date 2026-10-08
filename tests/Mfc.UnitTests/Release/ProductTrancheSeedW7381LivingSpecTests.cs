using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-381: known-limitations / queue seed locked SNAP-ERRTEXT-CORR-01 (W7-382) after PLAN-59 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7381LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedSnapErrtextCorr01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan59 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-59-snapshot-failed-errortext-correlation.md"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("Intentional residual (W7-381 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-382", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-383", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-382**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-381 | [#1168](https://github.com/sesquicadaver/MTDirector/issues/1168) | Seed first PLAN-59 atomic row after inventory → SNAP-ERRTEXT-CORR-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-382 | [#1170](https://github.com/sesquicadaver/MTDirector/issues/1170) | SNAP-ERRTEXT-CORR-01 — Show capture-progress correlation id on Snapshot Failed-stage ErrorText | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-383 | [#1171](https://github.com/sesquicadaver/MTDirector/issues/1171) | Seed next after SNAP-ERRTEXT-CORR-01 (PLAN-59 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-381", plan, StringComparison.Ordinal);
        Assert.Contains("W7-382", plan, StringComparison.Ordinal);
        Assert.Contains("W7-383", plan, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan, StringComparison.Ordinal);

        Assert.Contains("W7-381 (#1168) DONE", plan59, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-382", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-383", plan59, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan59, StringComparison.Ordinal);

        Assert.Contains(
            "ErrorText = FormatCaptureProgress(failed)",
            viewer,
            StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex);", viewer, StringComparison.Ordinal);
        Assert.Contains("ErrorText = fault", viewer, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
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
