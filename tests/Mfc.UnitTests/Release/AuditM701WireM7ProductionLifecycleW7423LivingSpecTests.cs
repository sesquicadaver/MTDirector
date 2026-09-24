using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-423: AUDIT-M7-01 — wire M7 production lifecycle (audit F13).</summary>
public sealed class AuditM701WireM7ProductionLifecycleW7423LivingSpecTests
{
    [Fact]
    public void Ac1ProductionPathsWireBindTtlOutcomeDeployAlignAndCaptureRouting()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string bind = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Incident/IncidentResponseAssessmentUseCases.cs"));
        string jobExecutor = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Controller/Jobs/OperationalJobExecutor.cs"));
        string start = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs"));
        string deploy = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Incident/IncidentDenyOverlayDeployUseCases.cs"));
        string align = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Incident/IncidentCompileDevicePlanAlignment.cs"));
        string capture = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs"));
        string routingPort = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Abstractions/Jobs/IRoutingAssuranceCaptureProjectionPort.cs"));
        string rosPort = File.ReadAllText(
            Path.Combine(root, "src/Mfc.RouterOs/Jobs/RouterOsRoutingAssuranceCaptureProjectionPort.cs"));
        string di = File.ReadAllText(
            Path.Combine(root, "src/Mfc.RouterOs/DependencyInjection/RouterOsServiceCollectionExtensions.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("AUDIT-M7-01", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-423 (#1242) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-424 (#1244) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-425 (#1245) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-423 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-M7-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-423", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-M7-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("**DONE**", roadmap, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-423", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditM701WireM7ProductionLifecycleW7423", testing, StringComparison.Ordinal);

        Assert.Contains("IResponseAssessmentStore", bind, StringComparison.Ordinal);
        Assert.Contains("_assessments.SaveAsync", bind, StringComparison.Ordinal);
        Assert.Contains("ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase", jobExecutor, StringComparison.Ordinal);
        Assert.Contains("ExpiredExceptionReconciliation", jobExecutor, StringComparison.Ordinal);
        Assert.Contains("TryReportIncidentDeploymentOutcomeAsync", start, StringComparison.Ordinal);
        Assert.Contains("ReportIncidentDeploymentOutcomeUseCase?", start, StringComparison.Ordinal);
        Assert.Contains("IncidentCompileDevicePlanAlignment.EnsureMatch", deploy, StringComparison.Ordinal);
        Assert.Contains("INCIDENT_DEVICE_PLAN_COMPILE_MISMATCH", align, StringComparison.Ordinal);
        Assert.Contains("IRoutingAssuranceCaptureProjectionPort", capture, StringComparison.Ordinal);
        Assert.Contains("ProjectFromCapturePayloadsAsync", capture, StringComparison.Ordinal);
        Assert.Contains("NotConfiguredRoutingAssuranceCaptureProjectionPort", routingPort, StringComparison.Ordinal);
        Assert.Contains("UpsertAsync", rosPort, StringComparison.Ordinal);
        Assert.Contains(
            "AddScoped<IRoutingAssuranceCaptureProjectionPort, RouterOsRoutingAssuranceCaptureProjectionPort>",
            di,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryAddSingleton<IRoutingAssuranceCaptureProjectionPort, NotConfiguredRoutingAssuranceCaptureProjectionPort>",
            program,
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
