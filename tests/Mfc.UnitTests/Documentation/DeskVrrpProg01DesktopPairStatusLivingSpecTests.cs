using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-VRRP-PROG-01: VRRP pair status reuses <see cref="SnapshotViewerViewModel.FormatCaptureProgress"/>
/// so a Watch <c>ErrorDetail</c> correlation id, including incomplete capture, matches Snapshots and journald event 5301.
/// </summary>
public sealed class DeskVrrpProg01DesktopPairStatusLivingSpecTests
{
    [Fact]
    public void Ac1MemberStatusAndIncompleteCaptureReuseSnapshotCorrelationSuffix()
    {
        Guid correlation = Guid.Parse("44444444-4444-4444-4444-444444444444");
        CaptureProgress progress = new()
        {
            Stage = CaptureStage.Failed,
            Error = new ErrorDetail
            {
                Code = "failed",
                SanitizedDetail = "capture failed",
                CorrelationId = DesktopProtoUuid.FromGuid(correlation),
            },
        };

        string formatted = SnapshotViewerViewModel.FormatCaptureProgress(progress);
        Assert.Equal(
            "Failed: capture failed (correlation 44444444-4444-4444-4444-444444444444)",
            formatted);
        string status = $"member-a: {formatted}";
        Assert.Equal(
            "member-a: Failed: capture failed (correlation 44444444-4444-4444-4444-444444444444)",
            status);
        string incomplete =
            "Node capture did not complete successfully for all VRRP members. " + formatted;
        Assert.Contains("(correlation 44444444-4444-4444-4444-444444444444)", incomplete, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2WatchLoopAndIncompletePathCallFormatCaptureProgressAndPriorLocksHold()
    {
        string root = RepoRoot();
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan57 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-57-vrrp-capture-progress-fault-text.md"));

        Assert.Contains(
            "VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\"",
            node,
            StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(last)", node, StringComparison.Ordinal);
        Assert.Contains(
            "Node capture did not complete successfully for all VRRP members.",
            node,
            StringComparison.Ordinal);
        Assert.DoesNotContain("VrrpPairStatusText = $\"{memberName}: {progress.Stage}\"", node, StringComparison.Ordinal);
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex);", node, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\";", node, StringComparison.Ordinal);
        Assert.Equal(1, Count(node, "VrrpPairStatusText = \"VRRP pair consistency failed.\""));
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("public static string FormatCaptureProgress(CaptureProgress progress)", viewer, StringComparison.Ordinal);
        Assert.Contains("public static string Format(RpcException exception)", fault, StringComparison.Ordinal);
        Assert.Equal(2, Count(connection, "DesktopRpcFaultText.Format(ex)"));
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);

        Assert.Contains("DESK-VRRP-PROG-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01", local, StringComparison.Ordinal);
        Assert.Contains("DeskVrrpProg01DesktopPairStatusLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-374 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-PROG-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-374)", plan57, StringComparison.Ordinal);
        Assert.Contains("W7-374 (#1154) DONE", plan57, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-379 (#1163)", plan57, StringComparison.Ordinal);
        Assert.Contains(
            "W7-374 | [#1154](https://github.com/sesquicadaver/MTDirector/issues/1154) | DESK-VRRP-PROG-01 — Show capture-progress correlation id on VRRP pair status | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-375 | [#1155](https://github.com/sesquicadaver/MTDirector/issues/1155) | Seed next after DESK-VRRP-PROG-01 (PLAN-57 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-379 (#1163)", roadmap, StringComparison.Ordinal);
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
