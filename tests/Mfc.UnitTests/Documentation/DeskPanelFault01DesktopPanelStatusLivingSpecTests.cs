using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// DESK-PANEL-FAULT-01: panel status lines repeat DesktopRpcFaultText so operators
/// can join Drift, Audit, Incident, Routing assurance, and GetNodeWorkflow to ErrorText
/// and journald event 5301.
/// </summary>
public sealed class DeskPanelFault01DesktopPanelStatusLivingSpecTests
{
    [Fact]
    public void Ac1PanelStatusUsesTheSameFaultTextAsErrorText()
    {
        Guid correlation = Guid.Parse("44444444-4444-4444-4444-444444444444");
        ErrorDetail detail = new()
        {
            Code = "failed",
            Retryable = false,
            CorrelationId = DesktopProtoUuid.FromGuid(correlation),
            SanitizedDetail = "drift failed",
        };
        RpcException exception = new(
            new Status(StatusCode.FailedPrecondition, "drift failed"),
            new Metadata
            {
                { DesktopRpcFaultText.ErrorDetailMetadataKey, detail.ToByteArray() },
            });
        string fault = DesktopRpcFaultText.Format(exception);
        Assert.Equal(
            "Drift load failed. failed (correlation 44444444-4444-4444-4444-444444444444): drift failed",
            $"Drift load failed. {fault}");
        Assert.Equal(
            "GetNodeWorkflow failed. failed (correlation 44444444-4444-4444-4444-444444444444): drift failed",
            $"GetNodeWorkflow failed. {fault}");
    }

    [Fact]
    public void Ac2RpcCatchesCopyFaultTextAndPriorLocksHold()
    {
        string root = RepoRoot();
        string drift = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DriftViewModel.cs"));
        string audit = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/AuditViewModel.cs"));
        string incident = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/IncidentViewModel.cs"));
        string routing = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/RoutingAssuranceViewModel.cs"));
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
        string plan58 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-58-desktop-panel-status-fault-text.md"));

        Assert.Contains("StatusText = $\"Drift load failed. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"GetDriftEvent failed; showing list payload. {fault}\"", drift, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Audit load failed. {fault}\"", audit, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Incident ingest failed. {fault}\"", incident, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Incident assessment bind failed. {fault}\"", incident, StringComparison.Ordinal);
        Assert.Contains("StatusText = $\"Routing assurance load failed. {fault}\"", routing, StringComparison.Ordinal);
        Assert.Contains("DeploymentReadinessText = $\"GetNodeWorkflow failed. {fault}\"", node, StringComparison.Ordinal);
        Assert.Equal(1, Count(drift, "StatusText = \"Drift load failed.\""));
        Assert.Equal(1, Count(node, "DeploymentReadinessText = \"GetNodeWorkflow failed.\""));
        Assert.Contains("VrrpPairStatusText = $\"VRRP pair consistency failed. {fault}\"", node, StringComparison.Ordinal);
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

        Assert.Contains("DESK-PANEL-FAULT-01", profiles, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01", local, StringComparison.Ordinal);
        Assert.Contains("DeskPanelFault01DesktopPanelStatusLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-378 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-PANEL-FAULT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("Delivery notes (W7-378)", plan58, StringComparison.Ordinal);
        Assert.Contains("W7-378 (#1162) DONE", plan58, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", plan58, StringComparison.Ordinal);
        Assert.Contains(
            "W7-378 | [#1162](https://github.com/sesquicadaver/MTDirector/issues/1162) | DESK-PANEL-FAULT-01 — Show RPC fault text on panel status lines | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-379 | [#1163](https://github.com/sesquicadaver/MTDirector/issues/1163) | Seed next after DESK-PANEL-FAULT-01 (PLAN-58 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-390 (#1186)", roadmap, StringComparison.Ordinal);
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
