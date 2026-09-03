using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Integration;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Incident.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Incident;

public sealed class EmitResponseFeedbackCommand
{
    public required string Actor { get; init; }

    public required ResponseFeedbackEventKind Kind { get; init; }

    public required Guid IncidentId { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid CorrelationId { get; init; }

    public IReadOnlyList<Guid> DeviceIds { get; init; } = [];

    public byte[]? PolicyHash { get; init; }

    public byte[]? ArtifactHash { get; init; }

    public byte[]? PlanHash { get; init; }

    public string? VerificationResults { get; init; }

    public string? RollbackStatus { get; init; }

    public string? ResidualRisk { get; init; }
}

/// <summary>Persists and optionally delivers one RESPONSE_* feedback event (M7.4-05).</summary>
public sealed class EmitResponseFeedbackUseCase
{
    public const string Operation = "incident.response_feedback.emit";

    private readonly IAuthorizationBoundary _auth;
    private readonly IResponseFeedbackEventStore _store;
    private readonly IResponseFeedbackDeliveryPort _delivery;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EmitResponseFeedbackUseCase(
        IAuthorizationBoundary auth,
        IResponseFeedbackEventStore store,
        IResponseFeedbackDeliveryPort delivery,
        IAuditEventWriter audit,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _store = store;
        _delivery = delivery;
        _audit = audit;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ResponseFeedbackEventView>> ExecuteAsync(
        EmitResponseFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentFeedbackEmit,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ResponseFeedbackEvent feedbackEvent;
        try
        {
            feedbackEvent = ResponseFeedbackEvent.Create(
                command.Kind,
                new IncidentId(command.IncidentId),
                new NodeId(command.NodeId),
                command.DeviceIds.Select(static id => new DeviceId(id)),
                command.CorrelationId,
                _clock.UtcNow,
                ToHash(command.PolicyHash),
                ToHash(command.ArtifactHash),
                ToHash(command.PlanHash),
                command.VerificationResults,
                command.RollbackStatus,
                command.ResidualRisk);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        // Delivery is outside the DB boundary (may be NotConfigured). Outcome is captured first so the
        // atomic store+audit write can include it; ports used in-process are sync/fail-closed.
        ResponseFeedbackDeliveryResult delivery = await _delivery
            .DeliverAsync(feedbackEvent, cancellationToken)
            .ConfigureAwait(false);
        await _unitOfWork.ExecuteAsync(
            async ct =>
            {
                await _store.AppendAsync(feedbackEvent, ct).ConfigureAwait(false);
                await _audit.AppendAsync(
                        command.Actor,
                        Operation,
                        JsonSerializer.Serialize(new
                        {
                            event_id = feedbackEvent.Id.Value,
                            event_code = feedbackEvent.EventCode,
                            incident_id = feedbackEvent.IncidentId.Value,
                            node_id = feedbackEvent.NodeId.Value,
                            correlation_id = feedbackEvent.CorrelationId,
                            delivery = delivery.Outcome.ToString(),
                        }),
                        ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ResponseFeedbackEventView.FromDomain(feedbackEvent, delivery.Outcome));
    }

    private static Hash256? ToHash(byte[]? bytes)
        => bytes is null || bytes.Length == 0 ? null : Hash256.Create(bytes);
}

public sealed class ListResponseFeedbackEventsCommand
{
    public required string Actor { get; init; }

    public Guid? IncidentId { get; init; }

    public Guid? NodeId { get; init; }
}

/// <summary>Lists persisted RESPONSE_* feedback events for external pull (M7.4-05).</summary>
public sealed class ListResponseFeedbackEventsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IResponseFeedbackEventStore _store;

    public ListResponseFeedbackEventsUseCase(
        IAuthorizationBoundary auth,
        IResponseFeedbackEventStore store)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(store);
        _auth = auth;
        _store = store;
    }

    public async Task<ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>>> ExecuteAsync(
        ListResponseFeedbackEventsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth,
            command.Actor,
            ApplicationPermissions.IncidentFeedbackRead,
            cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        if (command.IncidentId is null && command.NodeId is null)
        {
            return ApplicationResults.Fail(ApplicationError.Validation("incident_id or node_id is required."));
        }

        IReadOnlyList<ResponseFeedbackEvent> events;
        if (command.IncidentId is not null)
        {
            events = await _store
                .ListByIncidentAsync(new IncidentId(command.IncidentId.Value), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            events = await _store
                .ListByNodeAsync(new NodeId(command.NodeId!.Value), cancellationToken)
                .ConfigureAwait(false);
        }

        ResponseFeedbackEventView[] views = events
            .Select(static e => ResponseFeedbackEventView.FromDomain(e, null))
            .ToArray();
        return ApplicationResults.Ok<IReadOnlyList<ResponseFeedbackEventView>>(views);
    }
}
