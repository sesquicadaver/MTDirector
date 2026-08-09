using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;

namespace Mfc.Application.Inventory;

/// <summary>
/// Delegates to <see cref="IConnectionProfileService"/> without exposing secrets in the result.
/// Audit is emitted by the connection-profile service (never includes password material).
/// </summary>
public sealed class UpdateConnectionProfileUseCase
{
    public const string Operation = "inventory.update_device_connection";

    private readonly IAuthorizationBoundary _auth;
    private readonly IConnectionProfileService _profiles;
    private readonly IIdempotencyStore _idempotency;

    public UpdateConnectionProfileUseCase(
        IAuthorizationBoundary auth,
        IConnectionProfileService profiles,
        IIdempotencyStore idempotency)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(idempotency);
        _auth = auth;
        _profiles = profiles;
        _idempotency = idempotency;
    }

    public async Task<ApplicationResult<ConnectionProfileView>> ExecuteAsync(
        UpsertConnectionProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await AuthorizationGuard.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.ConnectionProfileWrite, cancellationToken)
            .ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        // Password material is intentionally excluded from the idempotency digest.
        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.DeviceId,
            command.Username,
            trust = command.TrustMode.ToString(),
            command.CaProfileRef,
            pin = command.PinnedSpkiSha256?.ToString(),
            command.ConnectTimeoutMs,
            command.CommandTimeoutMs,
            command.MaxResponseBytes,
        });

        ApplicationResult<ConnectionProfileView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (deviceId, ct) =>
            {
                ConnectionProfileView? existing = await _profiles.GetViewAsync(deviceId, ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(
                        ApplicationError.NotFound($"Connection profile for device '{deviceId}' was not found."))
                    : ApplicationResults.Ok(existing);
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            ConnectionProfileView view = await _profiles.UpsertAsync(command, cancellationToken)
                .ConfigureAwait(false);
            await _idempotency.SaveAsync(
                    command.Actor,
                    Operation,
                    command.IdempotencyKey,
                    requestHash,
                    view.DeviceId,
                    cancellationToken)
                .ConfigureAwait(false);
            return ApplicationResults.Ok(view);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}
