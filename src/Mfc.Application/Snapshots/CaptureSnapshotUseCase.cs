using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Snapshots;

public sealed class CaptureSnapshotCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }

    /// <summary>Client-supplied idempotency key for capture_operations (M1-23 AC#7).</summary>
    public required Guid IdempotencyKey { get; init; }
}

/// <summary>
/// Captures a RouterOS snapshot and persists metadata + content-addressed payloads atomically (M1-23).
/// Identical snapshot hashes reuse the existing completed capture; the capture event is always audited.
/// </summary>
public sealed class CaptureSnapshotUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly ISnapshotCapturePort _capture;
    private readonly ISnapshotStore _snapshots;
    private readonly IAuditEventWriter _audit;

    public CaptureSnapshotUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        ISnapshotCapturePort capture,
        ISnapshotStore snapshots,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _devices = devices;
        _profiles = profiles;
        _capture = capture;
        _snapshots = snapshots;
        _audit = audit;
    }

    public async Task<ApplicationResult<SnapshotView>> ExecuteAsync(
        CaptureSnapshotCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.SnapshotCapture, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        if (command.IdempotencyKey == Guid.Empty)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed("IdempotencyKey must be a non-empty GUID."));
        }

        Device? device = await _devices.GetAsync(new DeviceId(command.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device '{command.DeviceId}' not found."));
        }

        Guid requestedBy = ActorKey.FromActor(command.Actor);
        StoredSnapshot? byIdempotency = await _snapshots
            .FindByIdempotencyAsync(requestedBy, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (byIdempotency is not null)
        {
            await AuditAsync(
                command.Actor,
                "snapshot.capture.idempotent",
                byIdempotency,
                identical: true,
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(byIdempotency, deduplicated: true));
        }

        ConnectionProfileReadModel? profile = await _profiles.GetAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed($"Connection profile for device '{command.DeviceId}' is missing."));
        }

        RouterOsReadTarget target = new()
        {
            DeviceId = device.Id,
            Endpoint = device.ManagementEndpoint,
            SecretReference = profile.SecretReference,
            TrustMode = profile.TrustMode,
            CaProfileRef = profile.CaProfileRef,
            PinnedSpkiSha256 = profile.PinnedSpkiSha256,
        };

        SnapshotCaptureResult captured;
        try
        {
            captured = await _capture.CaptureAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ApplicationResults.Fail(
                ApplicationError.Dependency("Snapshot capture failed (sanitized)."));
        }

        StoredSnapshot? existing = await _snapshots
            .FindCompletedBySnapshotHashAsync(device.Id, captured.SnapshotHash, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            await AuditAsync(
                command.Actor,
                "snapshot.capture.identical",
                existing,
                identical: true,
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(existing, deduplicated: true));
        }

        StoredSnapshot stored = await _snapshots.PersistCompletedAsync(
            new SnapshotPersistRequest
            {
                DeviceId = device.Id,
                RequestedBy = requestedBy,
                IdempotencyKey = command.IdempotencyKey,
                Capture = captured,
                CapturedAtUtc = DateTimeOffset.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);

        await AuditAsync(
            command.Actor,
            "snapshot.capture.completed",
            stored,
            identical: false,
            cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(ViewMapper.ToView(stored, deduplicated: false));
    }

    private async Task AuditAsync(
        string actor,
        string action,
        StoredSnapshot snapshot,
        bool identical,
        CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(new
        {
            snapshotId = snapshot.Metadata.Id.Value,
            deviceId = snapshot.Metadata.DeviceId.Value,
            snapshotHash = snapshot.Metadata.SnapshotHash?.ToString(),
            configurationHash = snapshot.Metadata.ConfigurationHash?.ToString(),
            observationHash = snapshot.Metadata.ObservationHash?.ToString(),
            identical,
        });
        await _audit.AppendAsync(actor, action, payload, cancellationToken).ConfigureAwait(false);
    }
}
