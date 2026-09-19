using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-SVC-FAULT-01: snapshot viewer, snapshot diff, and inventory refresh store
/// DesktopRpcFaultText.Format on RpcException so shell Error includes the correlation id.
/// </summary>
public sealed class DeskSvcFault01DesktopServiceErrorLivingSpecTests
{
    [Fact]
    public void Ac1RpcExceptionKeepsCorrelationAndOtherExceptionsKeepMessage()
    {
        Guid correlation = Guid.Parse("33333333-3333-3333-3333-333333333333");
        ErrorDetail detail = new()
        {
            Code = "snapshot",
            SanitizedDetail = "list failed",
            CorrelationId = DesktopProtoUuid.FromGuid(correlation),
        };
        Exception rpc = new RpcException(
            new Status(StatusCode.Internal, "list failed"),
            new Metadata { { DesktopRpcFaultText.ErrorDetailMetadataKey, detail.ToByteArray() } });
        Assert.Equal(
            "snapshot (correlation 33333333-3333-3333-3333-333333333333): list failed",
            DesktopRpcFaultText.Format(rpc));

        Exception other = new InvalidOperationException("simulated inventory failure");
        Assert.Equal("simulated inventory failure", DesktopRpcFaultText.Format(other));
    }

    [Fact]
    public void Ac2ServiceErrorUsesFormatAndPriorLocksHold()
    {
        string root = RepoRoot();
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
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
        string plan60 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-60-desktop-service-rpc-fault-text.md"));

        Assert.Equal(3, Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(2, Count(diff, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(1, Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Equal(0, Count(viewerService, "Error = ex.Message"));
        Assert.Equal(0, Count(diff, "Error = ex.Message"));
        Assert.Equal(0, Count(inventory, "Error = ex.Message"));
        Assert.Contains("if (exception is RpcException rpc)", helper, StringComparison.Ordinal);
        Assert.Contains("return Format(rpc);", helper, StringComparison.Ordinal);
        Assert.Contains("return exception.Message;", helper, StringComparison.Ordinal);

        Assert.Contains("ErrorText = FormatCaptureProgress(failed)", viewer, StringComparison.Ordinal);
        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex);", viewer, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Contains("SnapshotViewerViewModel.FormatCaptureProgress(progress)", node, StringComparison.Ordinal);
        Assert.Equal(4, Count(connection, "DesktopRpcFaultText.Format(ex)"));
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);

        Assert.Contains("DESK-SVC-FAULT-01", snapshots, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01", local, StringComparison.Ordinal);
        Assert.Contains("DeskSvcFault01DesktopServiceErrorLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-386 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-SVC-FAULT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-386 (#1178) DONE", plan60, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-392 (#1191)", plan60, StringComparison.Ordinal);
        Assert.Contains(
            "W7-386 | [#1178](https://github.com/sesquicadaver/MTDirector/issues/1178) | DESK-SVC-FAULT-01 — Store DesktopRpcFaultText.Format on service Error for RpcException | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-387 | [#1179](https://github.com/sesquicadaver/MTDirector/issues/1179) | Seed next after DESK-SVC-FAULT-01 (PLAN-60 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-392 (#1191)", roadmap, StringComparison.Ordinal);
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
