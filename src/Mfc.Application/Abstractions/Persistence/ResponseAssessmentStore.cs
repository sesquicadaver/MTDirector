using Mfc.Domain.Endpoint;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Persists endpoint incident response assessments (M7.2-03).</summary>
public interface IResponseAssessmentStore
{
    Task<ResponseAssessment?> GetActiveByEndpointAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(ResponseAssessment assessment, CancellationToken cancellationToken = default);
}
