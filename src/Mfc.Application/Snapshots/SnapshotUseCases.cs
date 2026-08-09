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

// CaptureSnapshotUseCase lives in CaptureSnapshotUseCase.cs (M1-23).

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

    /// <summary>Page size (1..200). Defaults to 50.</summary>
    public int Limit { get; init; } = 50;

    /// <summary>Opaque cursor from a previous page.</summary>
    public string? Cursor { get; init; }
}

public sealed class SnapshotListPageView
{
    public required IReadOnlyList<SnapshotView> Items { get; init; }

    public string? NextCursor { get; init; }
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

    public async Task<ApplicationResult<SnapshotListPageView>> ExecuteAsync(
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

        int limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        StoredSnapshotPage page = await _snapshots
            .ListByDevicePageAsync(new DeviceId(query.DeviceId), limit, query.Cursor, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok(new SnapshotListPageView
        {
            Items = page.Items.Select(ViewMapper.ToView).ToArray(),
            NextCursor = page.NextCursor,
        });
    }
}

public sealed class GetRawSnapshotPayloadQuery
{
    public required string Actor { get; init; }

    public required Guid SnapshotId { get; init; }
}

/// <summary>
/// Returns the sanitized raw payload for a capture. Requires <see cref="ApplicationPermissions.SnapshotRawRead"/>
/// in addition to ordinary snapshot.read (M1-23 AC#11).
/// </summary>
public sealed class GetRawSnapshotPayloadUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public GetRawSnapshotPayloadUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<StoredSnapshotPayload>> ExecuteAsync(
        GetRawSnapshotPayloadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? readError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (readError is not null)
        {
            return ApplicationResults.Fail(readError);
        }

        ApplicationError? rawError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRawRead, cancellationToken).ConfigureAwait(false);
        if (rawError is not null)
        {
            return ApplicationResults.Fail(rawError);
        }

        StoredSnapshot? snapshot = await _snapshots.GetAsync(new SnapshotId(query.SnapshotId), cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Snapshot '{query.SnapshotId}' not found."));
        }

        if (snapshot.RawPayloadHash is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed("Snapshot has no raw payload."));
        }

        StoredSnapshotPayload? payload = await _snapshots
            .GetPayloadAsync(snapshot.RawPayloadHash, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound("Raw payload was not found."));
        }

        return ApplicationResults.Ok(payload);
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
