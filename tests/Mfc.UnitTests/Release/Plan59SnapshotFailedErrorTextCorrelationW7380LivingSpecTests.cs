using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-380: PLAN-59 inventory documents sole SNAP-ERRTEXT-CORR-01 rank
/// (Failed-stage ErrorText still writes SanitizedDetail only while FormatCaptureProgress already shows the correlation id)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan59SnapshotFailedErrorTextCorrelationW7380LivingSpecTests
{
    [Fact]
    public void Ac1Plan59InventoryDocumentsSoleSnapErrtextCorr01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan59 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-59-snapshot-failed-errortext-correlation.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));

        Assert.Contains("PLAN-59 — Snapshot failed-stage ErrorText correlation after panel status fault text", plan59, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan59, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", plan59, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan59, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("6b0b3f95", plan59, StringComparison.Ordinal);
        Assert.Contains("fc4fb991", plan59, StringComparison.Ordinal);
        Assert.Contains("SanitizedDetail", plan59, StringComparison.Ordinal);
        Assert.Contains("FormatCaptureProgress", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-380", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-381", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-382", plan59, StringComparison.Ordinal);
        Assert.Contains("W7-383", plan59, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", plan59, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-380 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-382", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-381", limitations, StringComparison.Ordinal);
        Assert.Contains("fc4fb991", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-380 | [#1167](https://github.com/sesquicadaver/MTDirector/issues/1167) | PLAN-59 — Inventory Snapshot failed-stage ErrorText correlation | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-403 (#1209)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-381", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-382", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-59", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-59-snapshot-failed-errortext-correlation.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-59-snapshot-failed-errortext-correlation.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan59SnapshotFailedErrorTextCorrelationW7380", testing, StringComparison.Ordinal);

        Assert.Equal(0, Count(viewer, "ErrorText = outcome.LastProgress.Error?.SanitizedDetail ?? \"Capture failed.\""));
        Assert.Contains("ErrorText = FormatCaptureProgress(failed)", viewer, StringComparison.Ordinal);
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
