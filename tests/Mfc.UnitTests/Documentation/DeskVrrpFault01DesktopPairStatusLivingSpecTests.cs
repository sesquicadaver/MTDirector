using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-VRRP-FAULT-01: VRRP pair status repeats DesktopRpcFaultText so operators
/// can join the status line to ErrorText and journald event 5301.
/// </summary>
public sealed class DeskVrrpFault01DesktopPairStatusLivingSpecTests
{
    [Fact]
    public void Ac1PairStatusUsesTheSameFaultTextAsErrorText()
    {
        Guid correlation = Guid.Parse("33333333-3333-3333-3333-333333333333");
        ErrorDetail detail = new()
        {
            Code = "failed",
            Retryable = false,
            CorrelationId = DesktopProtoUuid.FromGuid(correlation),
            SanitizedDetail = "capture failed",
        };
        RpcException exception = new(
            new Status(StatusCode.FailedPrecondition, "capture failed"),
            new Metadata
            {
                { DesktopRpcFaultText.ErrorDetailMetadataKey, detail.ToByteArray() },
            });
        string fault = DesktopRpcFaultText.Format(exception);
        string status = $"VRRP pair consistency failed. {fault}";
        Assert.Equal(
            "VRRP pair consistency failed. failed (correlation 33333333-3333-3333-3333-333333333333): capture failed",
            status);
    }

    [Fact]
    public void Ac2RpcCatchCopiesFaultTextAndPriorLocksHold()
    {
        string root = RepoRoot();
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/NodeDetailViewModel.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string viewer = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/SnapshotViewerViewModel.cs"));
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string unary = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string profiles = File.ReadAllText(Path.Combine(root, "docs/development/connection-profiles.md"));
        string local = File.ReadAllText(Path.Combine(root, "docs/development/local-environment.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan56 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-56-vrrp-pair-status-fault-text.md"));

        Assert.Contains("string fault = DesktopRpcFaultText.Format(ex);", node, StringComparison.Ordinal);
        Assert.Contains("ErrorText = fault;", node, StringComparison.Ordinal);
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\";", node, StringComparison.Ordinal);
        Assert.Equal(1, Count(node, "VrrpPairStatusText = \"VRRP pair consistency failed.\""));
        Assert.Contains("VrrpPairStatusText = $\"{memberName}: {SnapshotViewerViewModel.FormatCaptureProgress(progress)}\"", node, StringComparison.Ordinal);
        Assert.Contains("public static string Format(RpcException exception)", fault, StringComparison.Ordinal);

        int sites = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "src/Mfc.Desktop/ViewModels"), "*ViewModel.cs"))
        {
            sites += Count(File.ReadAllText(path), "DesktopRpcFaultText.Format");
        }

        Assert.Equal(14, sites);
        Assert.Equal(2, Count(connection, "DesktopRpcFaultText.Format(ex)"));
        Assert.Contains("(correlation {correlation})", viewer, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", unary, StringComparison.Ordinal);

        Assert.Contains("DESK-VRRP-FAULT-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01", local, StringComparison.Ordinal);
        Assert.Contains("DeskVrrpFault01DesktopPairStatusLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-370 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-VRRP-FAULT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-370)", plan56, StringComparison.Ordinal);
        Assert.Contains("W7-370 (#1146) DONE", plan56, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-382 (#1170)", plan56, StringComparison.Ordinal);
        Assert.Contains(
            "W7-370 | [#1146](https://github.com/sesquicadaver/MTDirector/issues/1146) | DESK-VRRP-FAULT-01 — Show RPC fault text on VRRP pair status | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-371 | [#1147](https://github.com/sesquicadaver/MTDirector/issues/1147) | Seed next after DESK-VRRP-FAULT-01 (PLAN-56 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-382 (#1170)", roadmap, StringComparison.Ordinal);
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
