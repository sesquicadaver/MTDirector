using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Snapshots;

public sealed class CoordinateStableReadCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

/// <summary>
/// Application entry for stable-read coordination (M1-19).
/// Does not persist snapshots — persistence of complete captures remains on capture use cases.
/// Unstable outcomes are returned as typed errors and never marked complete.
/// </summary>
public sealed class CoordinateStableReadUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IStableReadCoordinatorPort _coordinator;

    public CoordinateStableReadUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        IStableReadCoordinatorPort coordinator)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(coordinator);
        _auth = auth;
        _devices = devices;
        _profiles = profiles;
        _coordinator = coordinator;
    }

    public async Task<ApplicationResult<StableReadCoordinationResult>> ExecuteAsync(
        CoordinateStableReadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.SnapshotCapture, cancellationToken)
            .ConfigureAwait(false);
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

        StableReadCoordinationResult result = await _coordinator
            .CoordinateAsync(target, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(result.Outcome, StableReadOutcomeCodes.SnapshotUnstable, StringComparison.Ordinal))
        {
            return ApplicationResults.Fail(ApplicationError.SnapshotUnstable());
        }

        if (string.Equals(result.Outcome, StableReadOutcomeCodes.Canceled, StringComparison.Ordinal))
        {
            return ApplicationResults.Fail(ApplicationError.Failed("Stable-read was canceled."));
        }

        if (!result.IsComplete)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed("Stable-read did not produce a complete discovery dataset."));
        }

        return ApplicationResults.Ok(result);
    }
}
