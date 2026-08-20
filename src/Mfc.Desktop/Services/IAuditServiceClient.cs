using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only audit list client (M6-04). Read-only.</summary>
public interface IAuditServiceClient
{
    Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
        uint pageSize = 100,
        CancellationToken cancellationToken = default);
}
