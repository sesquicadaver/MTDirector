using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Drift;
using Mfc.Domain.Drift.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Drift;

/// <summary>Input finding for <see cref="DetectManagedDriftCommand"/>.</summary>
public sealed class DriftFindingInput
{
    public required DriftFindingKind Kind { get; init; }

    public string? Detail { get; init; }
}

/// <summary>Detects managed RouterOS configuration drift against last committed artifact (M6-02).</summary>
public sealed class DetectManagedDriftCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }

    /// <summary>Optional observed actual hash; when null, uses persisted DeviceHashState.actual.</summary>
    public string? ActualManagedResourceHashHex { get; init; }

    /// <summary>
    /// Optional desired hash for pending-deploy discrimination only — never used as drift baseline.
    /// When null, uses persisted DeviceHashState.desired.
    /// </summary>
    public string? DesiredArtifactHashHex { get; init; }

    public IReadOnlyList<DriftFindingInput> Findings { get; init; } = [];

    public string? SemanticDiffCanonical { get; init; }

    /// <summary>When true, persists ActualManagedResourceHashHex onto DeviceHashState.</summary>
    public bool PersistActualHash { get; init; }
}

/// <summary>
/// Compares actual managed state to last committed artifact, classifies findings, persists immutable DriftEvent + audit.
/// Does not write RouterOS and does not offer auto-repair.
/// </summary>
public sealed class DetectManagedDriftUseCase
{
    public const string AuditAction = DriftCodes.Detected;

    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IDeviceHashStateStore _hashStates;
    private readonly IDriftEventStore _driftEvents;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;

    public DetectManagedDriftUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IDeviceHashStateStore hashStates,
        IDriftEventStore driftEvents,
        IAuditEventWriter audit,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(driftEvents);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _devices = devices;
        _hashStates = hashStates;
        _driftEvents = driftEvents;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ApplicationResult<DriftEventView>> ExecuteAsync(
        DetectManagedDriftCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        DeviceId deviceId = new(command.DeviceId);
        Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Device '{command.DeviceId}' not found."));
        }

        DeviceHashState? hashState = await _hashStates.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (hashState is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device hash state '{command.DeviceId}' not found."));
        }

        Hash256? actual;
        Hash256? desired;
        try
        {
            actual = string.IsNullOrWhiteSpace(command.ActualManagedResourceHashHex)
                ? hashState.ActualManagedResourceHash
                : Hash256.ParseHex(command.ActualManagedResourceHashHex);
            desired = string.IsNullOrWhiteSpace(command.DesiredArtifactHashHex)
                ? hashState.DesiredArtifactHash
                : Hash256.ParseHex(command.DesiredArtifactHashHex);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        List<DriftFinding> findings = [];
        foreach (DriftFindingInput input in command.Findings)
        {
            findings.Add(new DriftFinding(input.Kind, input.Detail));
        }

        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            hashState.LastCommittedArtifactHash,
            actual,
            desired,
            findings,
            command.SemanticDiffCanonical);

        DateTimeOffset now = _clock.UtcNow;
        DriftEvent driftEvent = DriftEvent.Create(deviceId, device.NodeId, evaluation, now);
        await _driftEvents.AppendAsync(driftEvent, cancellationToken).ConfigureAwait(false);

        if (command.PersistActualHash && actual is not null)
        {
            DeviceHashState updated = hashState.With(
                hashState.DesiredPolicyHash,
                hashState.DesiredArtifactHash,
                hashState.LastCommittedPolicyHash,
                hashState.LastCommittedArtifactHash,
                actual,
                actualKnown: true,
                hashState.AnchorKnown,
                now);
            await _hashStates.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
        }

        await _audit.AppendAsync(
            command.Actor,
            AuditAction,
            JsonSerializer.Serialize(new
            {
                drift_event_id = driftEvent.Id.Value,
                device_id = deviceId.Value,
                node_id = device.NodeId.Value,
                outcome = driftEvent.Outcome.ToString(),
                configuration_drift = driftEvent.ConfigurationDriftPresent,
                blocks_deployment = driftEvent.BlocksDeployment,
                baseline_committed = driftEvent.BaselineCommittedHash?.ToString(),
                actual = driftEvent.ActualManagedResourceHash?.ToString(),
                desired_ignored_for_baseline = driftEvent.DesiredArtifactHashIgnoredForBaseline?.ToString(),
                semantic_diff_hash = driftEvent.SemanticDiffHash?.ToString(),
                finding_kinds = driftEvent.Findings.Select(static f => f.Kind.ToString()).ToArray(),
                immutable = true,
            }),
            cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(DriftViewMapper.ToView(driftEvent));
    }
}

/// <summary>Loads one immutable drift event.</summary>
public sealed class GetDriftEventQuery
{
    public required string Actor { get; init; }

    public required Guid DriftEventId { get; init; }
}

/// <summary>Reads a persisted drift event (inventory.read).</summary>
public sealed class GetDriftEventUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDriftEventStore _driftEvents;

    public GetDriftEventUseCase(IAuthorizationBoundary auth, IDriftEventStore driftEvents)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(driftEvents);
        _auth = auth;
        _driftEvents = driftEvents;
    }

    public async Task<ApplicationResult<DriftEventView>> ExecuteAsync(
        GetDriftEventQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        DriftEvent? driftEvent = await _driftEvents
            .GetAsync(new DriftEventId(query.DriftEventId), cancellationToken)
            .ConfigureAwait(false);
        if (driftEvent is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Drift event '{query.DriftEventId}' not found."));
        }

        return ApplicationResults.Ok(DriftViewMapper.ToView(driftEvent));
    }
}

/// <summary>Lists drift events for a device (newest first).</summary>
public sealed class ListDeviceDriftEventsQuery
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

/// <summary>Lists immutable drift events for a device (inventory.read).</summary>
public sealed class ListDeviceDriftEventsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDriftEventStore _driftEvents;

    public ListDeviceDriftEventsUseCase(IAuthorizationBoundary auth, IDriftEventStore driftEvents)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(driftEvents);
        _auth = auth;
        _driftEvents = driftEvents;
    }

    public async Task<ApplicationResult<IReadOnlyList<DriftEventView>>> ExecuteAsync(
        ListDeviceDriftEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        IReadOnlyList<DriftEvent> events = await _driftEvents
            .ListByDeviceAsync(new DeviceId(query.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok<IReadOnlyList<DriftEventView>>(
            events.Select(DriftViewMapper.ToView).ToArray());
    }
}

internal static class DriftViewMapper
{
    public static DriftEventView ToView(DriftEvent driftEvent)
        => new()
        {
            Id = driftEvent.Id.Value,
            DeviceId = driftEvent.DeviceId.Value,
            NodeId = driftEvent.NodeId.Value,
            BaselineCommittedHashHex = driftEvent.BaselineCommittedHash?.ToString(),
            ActualManagedResourceHashHex = driftEvent.ActualManagedResourceHash?.ToString(),
            DesiredArtifactHashIgnoredForBaselineHex =
                driftEvent.DesiredArtifactHashIgnoredForBaseline?.ToString(),
            Outcome = driftEvent.Outcome,
            ConfigurationDriftPresent = driftEvent.ConfigurationDriftPresent,
            BlocksDeployment = driftEvent.BlocksDeployment,
            Findings = driftEvent.Findings.Select(static f => new DriftFindingView
            {
                Kind = f.Kind,
                Severity = f.Severity,
                Detail = f.Detail,
            }).ToArray(),
            SemanticDiffCanonical = driftEvent.SemanticDiffCanonical,
            SemanticDiffHashHex = driftEvent.SemanticDiffHash?.ToString(),
            CreatedAtUtc = driftEvent.CreatedAtUtc,
            Immutable = driftEvent.Immutable,
        };
}
