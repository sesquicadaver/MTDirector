using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only IncidentService client (SEC-06: Ingest + BindAssessment).</summary>
public interface IIncidentServiceClient
{
    Task<IncidentSignal> IngestIncidentSignalAsync(
        IngestIncidentSignalRequest request,
        CancellationToken cancellationToken = default);

    Task<IncidentResponseAssessmentBinding> BindIncidentResponseAssessmentAsync(
        BindIncidentResponseAssessmentRequest request,
        CancellationToken cancellationToken = default);
}
