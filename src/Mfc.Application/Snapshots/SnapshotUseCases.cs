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

public sealed class DiscoverDeviceCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

/// <summary>
/// Read-only identity probe. Does not mutate RouterOS (Vertical Slice / AC #7).
/// Full discovery is performed by <see cref="CaptureSnapshotUseCase"/>.
/// </summary>
public sealed class DiscoverDeviceUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsReadPort _routerOs;

    public DiscoverDeviceUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        IRouterOsReadPort routerOs)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(routerOs);
        _auth = auth;
        _devices = devices;
        _profiles = profiles;
        _routerOs = routerOs;
    }

    public async Task<ApplicationResult<DeviceDiscoveryView>> ExecuteAsync(
        DiscoverDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DiscoveryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationResult<(Device Device, RouterOsReadTarget Target)> prepared =
            await PrepareTargetAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            return ApplicationResults.Fail(prepared.Error!);
        }

        (Device device, RouterOsReadTarget target) = prepared.Value!;
        RouterOsProbeResult probe = await _routerOs.ProbeAsync(target, cancellationToken).ConfigureAwait(false);
        device.RecordSupportState(probe.SupportState);
        await _devices.UpdateAsync(device, cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(new DeviceDiscoveryView
        {
            DeviceId = device.Id.Value,
            ObservedIdentity = probe.Identity,
            SupportState = probe.SupportState,
            RouterOsMutated = false,
        });
    }

    private async Task<ApplicationResult<(Device, RouterOsReadTarget)>> PrepareTargetAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        Device? device = await _devices.GetAsync(new DeviceId(deviceId), cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device '{deviceId}' not found."));
        }

        ConnectionProfileReadModel? profile = await _profiles.GetAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed($"Connection profile for device '{deviceId}' is missing."));
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
        return ApplicationResults.Ok((device, target));
    }
}

public sealed class CaptureSnapshotCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

public sealed class CaptureSnapshotUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly ISnapshotCapturePort _capture;
    private readonly ISnapshotStore _snapshots;

    public CaptureSnapshotUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        ISnapshotCapturePort capture,
        ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _devices = devices;
        _profiles = profiles;
        _capture = capture;
        _snapshots = snapshots;
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

        Device? device = await _devices.GetAsync(new DeviceId(command.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device '{command.DeviceId}' not found."));
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

        SnapshotCaptureResult captured = await _capture.CaptureAsync(target, cancellationToken)
            .ConfigureAwait(false);

        StoredSnapshot? existing = await _snapshots
            .FindCompletedBySnapshotHashAsync(device.Id, captured.SnapshotHash, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            // Idempotent: identical snapshot hash returns the existing capture.
            return ApplicationResults.Ok(ViewMapper.ToView(existing));
        }

        SnapshotMetadata metadata = SnapshotMetadata.CreateCompleted(
            device.Id,
            captured.ConfigurationHash,
            captured.ObservationHash,
            captured.CapabilityHash,
            captured.SnapshotHash,
            DateTimeOffset.UtcNow);

        StoredSnapshot stored = new()
        {
            Metadata = metadata,
            SchemaVersion = captured.SchemaVersion,
        };
        await _snapshots.AddAsync(stored, cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ViewMapper.ToView(stored));
    }
}

public sealed class GetSnapshotQuery
{
    public required string Actor { get; init; }

    public required Guid SnapshotId { get; init; }
}

public sealed class GetSnapshotUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public GetSnapshotUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<SnapshotView>> ExecuteAsync(
        GetSnapshotQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        StoredSnapshot? snapshot = await _snapshots.GetAsync(new SnapshotId(query.SnapshotId), cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Snapshot '{query.SnapshotId}' not found."));
        }

        return ApplicationResults.Ok(ViewMapper.ToView(snapshot));
    }
}

public sealed class ListSnapshotsQuery
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

public sealed class ListSnapshotsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public ListSnapshotsUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<IReadOnlyList<SnapshotView>>> ExecuteAsync(
        ListSnapshotsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        IReadOnlyList<StoredSnapshot> items = await _snapshots
            .ListByDeviceAsync(new DeviceId(query.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SnapshotView> views = items.Select(ViewMapper.ToView).ToArray();
        return ApplicationResults.Ok(views);
    }
}

public sealed class CompareSnapshotsQuery
{
    public required string Actor { get; init; }

    public required Guid LeftSnapshotId { get; init; }

    public required Guid RightSnapshotId { get; init; }
}

/// <summary>
/// Hash-level compare for M1-05. Full semantic section diff lands in M1-24.
/// </summary>
public sealed class CompareSnapshotsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public CompareSnapshotsUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<SnapshotDiffView>> ExecuteAsync(
        CompareSnapshotsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotCompare, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        StoredSnapshot? left = await _snapshots.GetAsync(new SnapshotId(query.LeftSnapshotId), cancellationToken)
            .ConfigureAwait(false);
        StoredSnapshot? right = await _snapshots.GetAsync(new SnapshotId(query.RightSnapshotId), cancellationToken)
            .ConfigureAwait(false);
        if (left is null || right is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound("One or both snapshots were not found."));
        }

        List<string> changed = [];
        if (!NullableHashEquals(left.Metadata.ConfigurationHash, right.Metadata.ConfigurationHash))
        {
            changed.Add("configuration_hash");
        }

        if (!NullableHashEquals(left.Metadata.ObservationHash, right.Metadata.ObservationHash))
        {
            changed.Add("observation_hash");
        }

        if (!NullableHashEquals(left.Metadata.CapabilityHash, right.Metadata.CapabilityHash))
        {
            changed.Add("capability_hash");
        }

        if (!NullableHashEquals(left.Metadata.SnapshotHash, right.Metadata.SnapshotHash))
        {
            changed.Add("snapshot_hash");
        }

        return ApplicationResults.Ok(new SnapshotDiffView
        {
            LeftSnapshotId = left.Metadata.Id.Value,
            RightSnapshotId = right.Metadata.Id.Value,
            Identical = changed.Count == 0,
            ChangedFields = changed,
        });
    }

    private static bool NullableHashEquals<T>(T? left, T? right)
        where T : struct, IEquatable<T>
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Value.Equals(right.Value);
    }
}
