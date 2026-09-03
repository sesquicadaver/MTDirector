using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Policy;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

public sealed class ValidateRevisionCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }
}

/// <summary>DRAFT → VALIDATED with CAS, idempotency, and audit (M2-18).</summary>
public sealed class ValidateRevisionUseCase
{
    public const string Operation = "policy.validate_revision";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ValidateRevisionUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        ValidateRevisionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.RevisionId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
        });
        ApplicationResult<PolicyRevisionView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            LoadRevisionViewAsync,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(_policies, command.RevisionId, cancellationToken)
            .ConfigureAwait(false);
        if (loadError is not null)
        {
            return ApplicationResults.Fail(loadError);
        }

        ApplicationError? cas = PolicyRevisionSupport.EnsureContentHash(revision!, command.ExpectedContentHash);
        if (cas is not null)
        {
            return ApplicationResults.Fail(cas);
        }

        try
        {
            revision!.MarkValidated();
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        await _unitOfWork.ExecuteAsync(
            async ct =>
            {
                await _policies.SaveRevisionAsync(revision, ct).ConfigureAwait(false);
                await _idempotency.SaveAsync(
                        command.Actor, Operation, command.IdempotencyKey, requestHash, revision.Id.Value, ct)
                    .ConfigureAwait(false);
                await _audit.AppendAsync(
                        command.Actor,
                        Operation,
                        JsonSerializer.Serialize(new { revision_id = revision.Id.Value, state = revision.State.ToString() }),
                        ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return await LoadRevisionViewAsync(revision.Id.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationResult<PolicyRevisionView>> LoadRevisionViewAsync(
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(_policies, revisionId, cancellationToken)
            .ConfigureAwait(false);
        if (loadError is not null)
        {
            return ApplicationResults.Fail(loadError);
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision!);
        if (document.IsFailure)
        {
            return ApplicationResults.Fail(document.Error!);
        }

        return ApplicationResults.Ok(ViewMapper.ToView(revision!, document.Value!));
    }
}
