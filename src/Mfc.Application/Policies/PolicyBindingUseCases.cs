using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

/// <summary>Activates a desired binding. Does not compile, write RouterOS, or start deployment.</summary>
public sealed class ActivateDesiredBindingUseCase
{
    public const string Operation = "policy.activate_desired_binding";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateDesiredBindingUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _approvals = approvals;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyBindingView>> ExecuteAsync(
        ActivateDesiredBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyBind, cancellationToken).ConfigureAwait(false);
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
            command.AnalysisRunId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
            fingerprint = Convert.ToHexString(command.CurrentDependencyFingerprint).ToLowerInvariant(),
        });
        ApplicationResult<PolicyBindingView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            LoadBindingViewAsync,
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

        Policy? policy = await _policies.GetPolicyAsync(revision!.PolicyId, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy '{revision.PolicyId}' was not found."));
        }

        PolicyAnalysisRun? run = await _approvals
            .GetAnalysisRunAsync(new PolicyAnalysisRunId(command.AnalysisRunId), cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return ApplicationResults.Fail(new ApplicationError(
                PolicyApprovalCodes.MissingRun,
                $"Analysis run '{command.AnalysisRunId}' was not found."));
        }

        ApplicationError? fpErr = PolicyRevisionSupport.TryHash(
            command.CurrentDependencyFingerprint, "current_dependency_fingerprint", out Hash256? fingerprint);
        if (fpErr is not null)
        {
            return ApplicationResults.Fail(fpErr);
        }

        PolicyBindingScope scope = PolicyDesiredBinding.ScopeFor(policy.Kind);
        Guid? scopeId = scope == PolicyBindingScope.Company ? null : policy.OwnerId;
        IReadOnlyList<PolicyDesiredBinding> existing = await _approvals
            .ListActiveBindingsAsync(scope, scopeId, cancellationToken)
            .ConfigureAwait(false);
        PolicyBindingEvaluation evaluation = PolicyBindingGate.EvaluateActivation(
            revision,
            policy,
            run,
            fingerprint!,
            existing);
        if (!evaluation.Allowed)
        {
            return ApplicationResults.Fail(new ApplicationError(
                evaluation.ErrorCode ?? PolicyApprovalCodes.BindingNotApproved,
                evaluation.ErrorMessage ?? "Binding rejected."));
        }

        DateTimeOffset now = _clock.UtcNow;
        DateTimeOffset? validFrom = null;
        DateTimeOffset? validUntil = null;
        if (policy.Kind == PolicyKind.Exception)
        {
            ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision);
            if (document.IsFailure)
            {
                return ApplicationResults.Fail(document.Error!);
            }

            ExceptionMetadata? metadata = document.Value!.ExceptionMetadata;
            if (metadata is null)
            {
                return ApplicationResults.Fail(new ApplicationError(
                    PolicyApprovalCodes.Blocker,
                    "EXCEPTION binding requires reason, ticket, and expiry metadata."));
            }

            validFrom = metadata.ValidFrom;
            validUntil = metadata.ValidUntil;
        }

        PolicyDesiredBinding binding;
        try
        {
            binding = PolicyDesiredBinding.Activate(policy, revision, run, now, validFrom, validUntil);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        List<PolicyDesiredBinding> replaced = [];
        IEnumerable<PolicyDesiredBinding> toReplace = scope == PolicyBindingScope.Exception
            ? existing.Where(b => b.State == PolicyBindingState.Active && b.PolicyId == policy.Id)
            : existing.Where(static b => b.State == PolicyBindingState.Active);
        foreach (PolicyDesiredBinding previous in toReplace)
        {
            previous.Disable(now);
            replaced.Add(previous);
        }

        try
        {
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    foreach (PolicyDesiredBinding previous in replaced)
                    {
                        await _approvals.SaveBindingAsync(previous, ct).ConfigureAwait(false);
                    }

                    await _approvals.AddBindingAsync(binding, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                        command.Actor, Operation, command.IdempotencyKey, requestHash, binding.Id.Value, ct)
                        .ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (PersistenceConflictException ex)
        {
            return ApplicationResults.Fail(new ApplicationError(ex.Code, ex.Message));
        }
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                binding_id = binding.Id.Value,
                revision_id = revision.Id.Value,
                state = binding.State.ToString(),
                deployment_started = false,
            }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ToView(binding));
    }

    private async Task<ApplicationResult<PolicyBindingView>> LoadBindingViewAsync(
        Guid bindingId,
        CancellationToken cancellationToken)
    {
        PolicyDesiredBinding? binding = await _approvals
            .GetBindingAsync(new PolicyBindingId(bindingId), cancellationToken)
            .ConfigureAwait(false);
        if (binding is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy binding '{bindingId}' not found."));
        }

        return ApplicationResults.Ok(ToView(binding));
    }

    internal static PolicyBindingView ToView(PolicyDesiredBinding binding)
        => new()
        {
            Id = binding.Id.Value,
            Scope = binding.Scope,
            ScopeId = binding.ScopeId,
            PolicyId = binding.PolicyId.Value,
            DesiredRevisionId = binding.DesiredRevisionId.Value,
            State = binding.State,
            RowVersion = binding.RowVersion,
            ValidUntilUtc = binding.ValidUntilUtc,
            DeploymentStarted = false,
        };
}

/// <summary>EXCEPTION expiry → EXPIRED_PENDING_RECONCILIATION. Deployed firewall is unchanged.</summary>
public sealed class ExpireExceptionBindingUseCase
{
    public const string Operation = "policy.expire_exception_binding";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IClock _clock;

    public ExpireExceptionBindingUseCase(
        IAuthorizationBoundary auth,
        IPolicyApprovalStore approvals,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _approvals = approvals;
        _idempotency = idempotency;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ApplicationResult<PolicyBindingView>> ExecuteAsync(
        ExpireExceptionBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyBind, cancellationToken).ConfigureAwait(false);
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
            command.BindingId,
            command.ExpectedRowVersion,
        });
        ApplicationResult<PolicyBindingView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (bindingId, ct) =>
            {
                PolicyDesiredBinding? existing = await _approvals
                    .GetBindingAsync(new PolicyBindingId(bindingId), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Policy binding '{bindingId}' not found."))
                    : ApplicationResults.Ok(ActivateDesiredBindingUseCase.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        PolicyDesiredBinding? binding = await _approvals
            .GetBindingAsync(new PolicyBindingId(command.BindingId), cancellationToken)
            .ConfigureAwait(false);
        if (binding is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy binding '{command.BindingId}' not found."));
        }

        if (binding.RowVersion != command.ExpectedRowVersion)
        {
            return ApplicationResults.Fail(ApplicationError.Conflict(
                "Policy binding row_version mismatch (expected_row_version CAS)."));
        }

        PolicyBindingEvaluation evaluation = PolicyBindingGate.EvaluateExpiry(binding, _clock.UtcNow);
        if (!evaluation.Allowed)
        {
            return ApplicationResults.Fail(new ApplicationError(
                evaluation.ErrorCode ?? PolicyApprovalCodes.BindingNotDue,
                evaluation.ErrorMessage ?? "Expiry rejected."));
        }

        try
        {
            binding.ExpirePendingReconciliation(_clock.UtcNow);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        await _approvals.SaveBindingAsync(binding, cancellationToken).ConfigureAwait(false);
        await _idempotency.SaveAsync(
            command.Actor, Operation, command.IdempotencyKey, requestHash, binding.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                binding_id = binding.Id.Value,
                state = binding.State.ToString(),
                deployment_started = false,
            }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ActivateDesiredBindingUseCase.ToView(binding));
    }
}
