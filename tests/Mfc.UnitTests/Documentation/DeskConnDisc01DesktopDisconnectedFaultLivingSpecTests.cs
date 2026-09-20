using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-CONN-DISC-01: connect and reconnect Disconnected catches store
/// DesktopRpcFaultText.Format on RpcException so shell status includes the correlation id.
/// </summary>
public sealed class DeskConnDisc01DesktopDisconnectedFaultLivingSpecTests
{
    [Fact]
    public void Ac1RpcExceptionKeepsCorrelationAndOtherExceptionsKeepMessage()
    {
        Guid correlation = Guid.Parse("44444444-4444-4444-4444-444444444444");
        ErrorDetail detail = new()
        {
            Code = "health",
            SanitizedDetail = "probe failed",
            CorrelationId = DesktopProtoUuid.FromGuid(correlation),
        };
        Exception rpc = new RpcException(
            new Status(StatusCode.Unavailable, "probe failed"),
            new Metadata { { DesktopRpcFaultText.ErrorDetailMetadataKey, detail.ToByteArray() } });
        Assert.Equal(
            "health (correlation 44444444-4444-4444-4444-444444444444): probe failed",
            DesktopRpcFaultText.Format(rpc));

        Exception other = new InvalidOperationException("simulated connect failure");
        Assert.Equal("simulated connect failure", DesktopRpcFaultText.Format(other));
    }

    [Fact]
    public void Ac2DisconnectedUsesFormatAndPriorLocksHold()
    {
        string root = RepoRoot();
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string viewerService = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotViewerService.cs"));
        string diff = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/SnapshotDiffService.cs"));
        string inventory = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/InventoryTreeService.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));

        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.Disconnected, DesktopRpcFaultText.Format(ex))"));
        Assert.Equal(0, Count(connection, "SetState(ControllerConnectionState.Disconnected, ex.Message)"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.TlsError, ex.Message)"));
        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.Disconnected, \"Health check timed out.\")"));
        Assert.Contains("if (exception is RpcException rpc)", helper, StringComparison.Ordinal);
        Assert.Contains("return Format(rpc);", helper, StringComparison.Ordinal);
        Assert.Contains("return exception.Message;", helper, StringComparison.Ordinal);

        Assert.Equal(
            6,
            Count(viewerService, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(diff, "Error = DesktopRpcFaultText.Format(ex)")
            + Count(inventory, "Error = DesktopRpcFaultText.Format(ex)"));
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);

        Assert.Contains("DESK-CONN-DISC-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01", local, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DeskConnDisc01DesktopDisconnectedFaultLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-390 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-DISC-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-390 (#1186) DONE", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", plan61, StringComparison.Ordinal);
        Assert.Contains(
            "W7-390 | [#1186](https://github.com/sesquicadaver/MTDirector/issues/1186) | DESK-CONN-DISC-01 — Store DesktopRpcFaultText.Format when Disconnected catches are RpcException | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-391 | [#1187](https://github.com/sesquicadaver/MTDirector/issues/1187) | Seed next after DESK-CONN-DISC-01 (PLAN-61 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", roadmap, StringComparison.Ordinal);
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
