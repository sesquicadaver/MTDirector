using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Snapshots;

public sealed class AssembleRawSnapshotCommand
{
    public required string Actor { get; init; }

    public required AssembleRawSnapshotRequest Request { get; init; }
}

/// <summary>
/// Assembles a versioned redacted raw snapshot (M1-20).
/// Does not persist; oversized payloads surface as typed application errors.
/// </summary>
public sealed class AssembleRawSnapshotUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IRawSnapshotAssemblerPort _assembler;

    public AssembleRawSnapshotUseCase(IAuthorizationBoundary auth, IRawSnapshotAssemblerPort assembler)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(assembler);
        _auth = auth;
        _assembler = assembler;
    }

    public async Task<ApplicationResult<RawSnapshotView>> ExecuteAsync(
        AssembleRawSnapshotCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.SnapshotCapture, cancellationToken)
            .ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        try
        {
            RawSnapshotView view = _assembler.Assemble(command.Request);
            return ApplicationResults.Ok(view);
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (SnapshotPayloadTooLargeException ex)
        {
            return ApplicationResults.Fail(ApplicationError.SnapshotTooLarge(ex.Message));
        }
    }
}
