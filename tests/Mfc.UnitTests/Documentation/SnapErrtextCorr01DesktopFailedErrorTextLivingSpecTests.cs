using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// SNAP-ERRTEXT-CORR-01: Failed-stage Snapshot ErrorText reuses FormatCaptureProgress
/// so the shell error shows the same correlation id as the progress line and journald event 5301.
/// </summary>
public sealed class SnapErrtextCorr01DesktopFailedErrorTextLivingSpecTests
{
    [Fact]
    public void Ac1FailedStageProgressLineIncludesCorrelationId()
    {
        Guid correlation = Guid.Parse("22222222-2222-2222-2222-222222222222");
        CaptureProgress progress = new()
        {
            Stage = CaptureStage.Failed,
            Error = new ErrorDetail
            {
                Code = "failed",
                SanitizedDetail = "device unreachable",
                CorrelationId = DesktopProtoUuid.FromGuid(correlation),
            },
        };

        Assert.Equal(
            "Failed: device unreachable (correlation 22222222-2222-2222-2222-222222222222)",
            SnapshotViewerViewModel.FormatCaptureProgress(progress));
    }

    [Fact]
    public void Ac2FailedStageErrorTextReusesProgressLineAndPriorLocksHold()
    {
        string root = RepoRoot();
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string snapshots = File.ReadAllText(Path.Combine(root, "docs/development/snapshots-and-diff.md"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan59 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-59-snapshot-failed-errortext-correlation.md"));

        Assert.Contains("ErrorText = FormatCaptureProgress(failed)", viewer, StringComparison.Ordinal);
        Assert.Equal(0, Count(viewer, "ErrorText = outcome.LastProgress.Error?.SanitizedDetail ?? \"Capture failed.\""));
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex);", viewer, StringComparison.Ordinal);
        Assert.Contains("ErrorText = fault", viewer, StringComparison.Ordinal);
        Assert.Contains("CaptureProgressText = $\"Failed: {fault}\"", viewer, StringComparison.Ordinal);
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
        Assert.Equal(4, Count(connection, "DesktopRpcFaultText.Format(ex)"));
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);

        Assert.Contains("SNAP-ERRTEXT-CORR-01", snapshots, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", profiles, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01", local, StringComparison.Ordinal);
        Assert.Contains("SnapErrtextCorr01DesktopFailedErrorTextLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-382 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-ERRTEXT-CORR-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-382 (#1170) DONE", plan59, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-425 (#1245)", plan59, StringComparison.Ordinal);
        Assert.Contains(
            "W7-382 | [#1170](https://github.com/sesquicadaver/MTDirector/issues/1170) | SNAP-ERRTEXT-CORR-01 — Show capture-progress correlation id on Snapshot Failed-stage ErrorText | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-383 | [#1171](https://github.com/sesquicadaver/MTDirector/issues/1171) | Seed next after SNAP-ERRTEXT-CORR-01 (PLAN-59 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-425 (#1245)", roadmap, StringComparison.Ordinal);
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
