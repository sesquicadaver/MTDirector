using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only routing assurance read client (M7.1-10).</summary>
public interface IRoutingAssuranceServiceClient
{
    Task<RoutingAssuranceStateDetail> GetDeviceRoutingAssuranceStateAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
