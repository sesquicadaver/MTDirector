using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class IngestIncidentSignalCommand
{
    public required string Actor { get; init; }

    public IReadOnlyList<string>? ForbiddenIngressFieldNames { get; init; }

    public required Guid EventId { get; init; }

    public required string SourceEventId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public required IncidentSignalSourceType SourceType { get; init; }

    public required string Category { get; init; }

    public required IncidentSeverity Severity { get; init; }

    public required int Confidence { get; init; }

    public required string DeduplicationKey { get; init; }

    public Guid? SiteId { get; init; }

    public Guid? NodeId { get; init; }

    public Guid? DeviceId { get; init; }

    public IReadOnlyList<EntityReference>? Entities { get; init; }

    public FlowTuple? Flow { get; init; }

    public FlowTuple? OriginalFlow { get; init; }

    public FlowTuple? TranslatedFlow { get; init; }

    public ushort? VlanId { get; init; }

    public string? Interface { get; init; }

    public string? Vrf { get; init; }

    public string? ContainerId { get; init; }

    public string? VpnIdentity { get; init; }

    public IReadOnlyList<Indicator>? Indicators { get; init; }

    public IReadOnlyList<string>? EvidenceRefs { get; init; }

    public string? RawEventRef { get; init; }
}

/// <summary>
/// Validates normalized incident signal ingress (M7.3-01). No raw syslog persistence; no signal store yet.
/// </summary>
public sealed class IngestIncidentSignalUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public IngestIncidentSignalUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<IncidentSignalView>> ExecuteAsync(
        IngestIncidentSignalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentSignalIngest,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            if (command.ForbiddenIngressFieldNames is not null)
            {
                IncidentSignalIngressGuard.RejectForbiddenIngressFieldNames(command.ForbiddenIngressFieldNames);
            }

            IncidentSignal signal = IncidentSignal.Create(
                new EventId(command.EventId),
                command.SourceEventId,
                command.OccurredAt,
                command.ReceivedAt,
                command.SourceType,
                command.Category,
                command.Severity,
                command.Confidence,
                command.DeduplicationKey,
                command.Entities,
                command.Flow,
                command.OriginalFlow,
                command.TranslatedFlow,
                command.SiteId is Guid siteId ? new SiteId(siteId) : null,
                command.NodeId is Guid nodeId ? new NodeId(nodeId) : null,
                command.DeviceId is Guid deviceId ? new DeviceId(deviceId) : null,
                command.VlanId,
                command.Interface,
                command.Vrf,
                command.ContainerId,
                command.VpnIdentity,
                command.Indicators,
                command.EvidenceRefs,
                command.RawEventRef);

            return ApplicationResults.Ok(IncidentSignalView.FromDomain(signal));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}
