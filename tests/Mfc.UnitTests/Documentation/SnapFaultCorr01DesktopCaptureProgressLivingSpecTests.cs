using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// SNAP-FAULT-CORR-01: one correlation id is shared by capture-progress ErrorDetail,
/// the RPC trailer, and event 5301, and is shown on the Desktop capture progress line.
/// </summary>
public sealed class SnapFaultCorr01DesktopCaptureProgressLivingSpecTests
{
    [Fact]
    public void Ac1PassedCorrelationIdIsTheTrailerIdAndEventTemplateIsUnchanged()
    {
        Guid id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        RpcException exception = GrpcApplicationErrorMapper.ToRpcException(
            ApplicationError.Failed("device unreachable"),
            id);
        byte[]? trailer = exception.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
        Assert.NotNull(trailer);
        ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
        Assert.Equal(id, ProtoUuid.ToGuid(detail.CorrelationId));
        Assert.Equal("failed", detail.Code);
        Assert.Equal("device unreachable", detail.SanitizedDetail);
        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
    }

    [Fact]
    public void Ac2CaptureSitesShareTheIdAndPriorLocksHold()
    {
        string root = RepoRoot();
        string snapshot = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string unary = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string snapshots = File.ReadAllText(Path.Combine(root, "docs/development/snapshots-and-diff.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan55 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-55-capture-progress-fault-correlation.md"));

        Assert.Equal(0, Count(snapshot, "CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid())"));
        Assert.Equal(2, Count(snapshot, "ToRpcException(result.Error!, sharedId)"));
        Assert.Equal(2, Count(snapshot, "NewCaptureFailureDetail(\"failed\", \"capture failed\", out _)"));
        Assert.Contains("sharedId = Guid.NewGuid()", snapshot, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);

        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains("CaptureProgressText = $\"Failed: {fault}\"", viewer, StringComparison.Ordinal);
        Assert.Contains("ErrorText = fault", viewer, StringComparison.Ordinal);
        Assert.Equal(4, Count(connection, "DesktopRpcFaultText.Format(ex)"));
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", unary, StringComparison.Ordinal);
        Assert.Contains("MinRequestBodyDataRate = null", program, StringComparison.Ordinal);

        Assert.Contains("SNAP-FAULT-CORR-01", snapshots, StringComparison.Ordinal);
        Assert.Contains("SnapFaultCorr01DesktopCaptureProgressLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-366 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("SNAP-FAULT-CORR-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-366 (#1138) DONE", plan55, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan55, StringComparison.Ordinal);
        Assert.Contains(
            "W7-366 | [#1138](https://github.com/sesquicadaver/MTDirector/issues/1138) | SNAP-FAULT-CORR-01 — Share capture progress correlation id with RPC fault | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-367 | [#1139](https://github.com/sesquicadaver/MTDirector/issues/1139) | Seed next after SNAP-FAULT-CORR-01 (PLAN-55 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", roadmap, StringComparison.Ordinal);
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
