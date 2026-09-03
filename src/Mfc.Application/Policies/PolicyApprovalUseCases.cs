using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

/// <summary>Caller-supplied finding for an analysis run (M2-17).</summary>
public sealed class PolicyApprovalFindingInput
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string Target { get; init; } = string.Empty;
}

/// <summary>Caller-supplied test outcome for an analysis run (M2-17).</summary>
public sealed class PolicyApprovalTestInput
{
    public required Guid TestId { get; init; }

    public required string Origin { get; init; }

    public required string Outcome { get; init; }

    public required string Proof { get; init; }
}

/// <summary>Records an immutable analysis run against a revision.</summary>
public sealed class RecordAnalysisRunCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required byte[] LogicalEffectiveHash { get; init; }

    public required byte[] AnalysisContextHash { get; init; }

    public required byte[] EvidenceContextHash { get; init; }

    public required byte[] TopologyProjectionHash { get; init; }

    public required byte[] ImpactSetHash { get; init; }

    public required IReadOnlyList<byte[]> PerDeviceAnalysisHashes { get; init; }

    public required byte[] DependencyFingerprint { get; init; }

    public required string RiskLevel { get; init; }

    public required bool EvidenceSignalsPresent { get; init; }

    public required string AnalyzerVersion { get; init; }

    public required string PolicySchemaVersion { get; init; }

    public required string PipelineVersion { get; init; }

    public required IReadOnlyList<PolicyApprovalFindingInput> Findings { get; init; }

    public required IReadOnlyList<PolicyApprovalTestInput> TestResults { get; init; }
}

/// <summary>Acknowledges one warning hash on an analysis run.</summary>
public sealed class AcknowledgeWarningCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required byte[] WarningHash { get; init; }
}

/// <summary>Transitions VALIDATED → IN_REVIEW.</summary>
public sealed class SubmitRevisionForReviewCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }
}

/// <summary>Records an approval vote bound to an analysis bundle hash.</summary>
public sealed class ApproveRevisionCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required byte[] ExpectedBundleHash { get; init; }

    public required byte[] CurrentDependencyFingerprint { get; init; }
}

/// <summary>Activates desired binding without starting deployment.</summary>
public sealed class ActivateDesiredBindingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required byte[] CurrentDependencyFingerprint { get; init; }
}

/// <summary>Expires an EXCEPTION binding without deploying.</summary>
public sealed class ExpireExceptionBindingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid BindingId { get; init; }

    public required ulong ExpectedRowVersion { get; init; }
}

/// <summary>Insert-only analysis run (Policy Model §67).</summary>
public sealed class RecordAnalysisRunUseCase
{
    public const string Operation = "policy.record_analysis_run";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public RecordAnalysisRunUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _policies = policies;
        _approvals = approvals;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<PolicyAnalysisRunView>> ExecuteAsync(
        RecordAnalysisRunCommand command,
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
            logical = Convert.ToHexString(command.LogicalEffectiveHash).ToLowerInvariant(),
            analysis = Convert.ToHexString(command.AnalysisContextHash).ToLowerInvariant(),
            evidence = Convert.ToHexString(command.EvidenceContextHash).ToLowerInvariant(),
            topology = Convert.ToHexString(command.TopologyProjectionHash).ToLowerInvariant(),
            impact = Convert.ToHexString(command.ImpactSetHash).ToLowerInvariant(),
            devices = command.PerDeviceAnalysisHashes.Select(static h => Convert.ToHexString(h).ToLowerInvariant()).ToArray(),
            fingerprint = Convert.ToHexString(command.DependencyFingerprint).ToLowerInvariant(),
            command.RiskLevel,
            command.EvidenceSignalsPresent,
            command.AnalyzerVersion,
            command.PolicySchemaVersion,
            command.PipelineVersion,
            findings = command.Findings.Select(static f => f.Code + "|" + f.Severity + "|" + f.Message + "|" + f.Target).ToArray(),
            tests = command.TestResults.Select(static t => t.TestId + "|" + t.Origin + "|" + t.Outcome + "|" + t.Proof).ToArray(),
        });
        ApplicationResult<PolicyAnalysisRunView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (runId, ct) => await LoadRunViewAsync(runId, ct).ConfigureAwait(false),
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

        ApplicationError? hashes = ParseRunHashes(command, out PolicyAnalysisRunHashes parsed);
        if (hashes is not null)
        {
            return ApplicationResults.Fail(hashes);
        }

        List<PolicyApprovalFinding> findings = [];
        foreach (PolicyApprovalFindingInput input in command.Findings)
        {
            try
            {
                findings.Add(new PolicyApprovalFinding
                {
                    Code = input.Code,
                    Severity = input.Severity,
                    Message = input.Message,
                    Target = input.Target,
                    WarningHash = PolicyApprovalHasher.HashWarning(input.Code, input.Target, input.Message),
                });
            }
            catch (DomainInvariantException ex)
            {
                return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
            }
        }

        List<PolicyApprovalTestOutcome> tests = command.TestResults.Select(static t => new PolicyApprovalTestOutcome
        {
            TestId = new PolicyTestId(t.TestId),
            Origin = t.Origin,
            Outcome = t.Outcome,
            Proof = t.Proof,
        }).ToList();

        PolicyAnalysisRun run;
        try
        {
            run = PolicyAnalysisRun.Create(
                revision!.Id,
                revision.ContentHash,
                parsed.Logical,
                parsed.Analysis,
                parsed.Evidence,
                parsed.Topology,
                parsed.Impact,
                parsed.Devices,
                parsed.Fingerprint,
                command.RiskLevel,
                command.EvidenceSignalsPresent,
                command.AnalyzerVersion,
                command.PolicySchemaVersion,
                command.PipelineVersion,
                findings,
                tests,
                new UserId(ActorKey.FromActor(command.Actor)),
                DateTimeOffset.UtcNow);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        await _approvals.AddAnalysisRunAsync(run, cancellationToken).ConfigureAwait(false);
        await _idempotency.SaveAsync(
            command.Actor, Operation, command.IdempotencyKey, requestHash, run.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                revision_id = revision.Id.Value,
                analysis_run_id = run.Id.Value,
                bundle_hash = run.BundleHash.ToString(),
                risk = run.EffectiveRiskLevel(),
            }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ToRunView(run));
    }

    private async Task<ApplicationResult<PolicyAnalysisRunView>> LoadRunViewAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        PolicyAnalysisRun? run = await _approvals
            .GetAnalysisRunAsync(new PolicyAnalysisRunId(runId), cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Analysis run '{runId}' not found."));
        }

        return ApplicationResults.Ok(ToRunView(run));
    }

    private static PolicyAnalysisRunView ToRunView(PolicyAnalysisRun run)
        => new()
        {
            Id = run.Id.Value,
            RevisionId = run.RevisionId.Value,
            BundleHashHex = run.BundleHash.ToString(),
            DependencyFingerprintHex = run.DependencyFingerprint.ToString(),
            RiskLevel = run.RiskLevel,
            EffectiveRiskLevel = run.EffectiveRiskLevel(),
            EvidenceSignalsPresent = run.EvidenceSignalsPresent,
        };

    private static ApplicationError? ParseRunHashes(RecordAnalysisRunCommand command, out PolicyAnalysisRunHashes parsed)
    {
        parsed = default;
        ApplicationError? err = PolicyRevisionSupport.TryHash(command.LogicalEffectiveHash, "logical_effective_hash", out Hash256? logical);
        if (err is not null)
        {
            return err;
        }

        err = PolicyRevisionSupport.TryHash(command.AnalysisContextHash, "analysis_context_hash", out Hash256? analysis);
        if (err is not null)
        {
            return err;
        }

        err = PolicyRevisionSupport.TryHash(command.EvidenceContextHash, "evidence_context_hash", out Hash256? evidence);
        if (err is not null)
        {
            return err;
        }

        err = PolicyRevisionSupport.TryHash(command.TopologyProjectionHash, "topology_projection_hash", out Hash256? topology);
        if (err is not null)
        {
            return err;
        }

        err = PolicyRevisionSupport.TryHash(command.ImpactSetHash, "impact_set_hash", out Hash256? impact);
        if (err is not null)
        {
            return err;
        }

        err = PolicyRevisionSupport.TryHash(command.DependencyFingerprint, "dependency_fingerprint", out Hash256? fingerprint);
        if (err is not null)
        {
            return err;
        }

        List<Hash256> devices = [];
        foreach (byte[] device in command.PerDeviceAnalysisHashes)
        {
            err = PolicyRevisionSupport.TryHash(device, "per_device_analysis_hash", out Hash256? hash);
            if (err is not null)
            {
                return err;
            }

            devices.Add(hash!);
        }

        parsed = new PolicyAnalysisRunHashes(logical!, analysis!, evidence!, topology!, impact!, devices, fingerprint!);
        return null;
    }

    private readonly record struct PolicyAnalysisRunHashes(
        Hash256 Logical,
        Hash256 Analysis,
        Hash256 Evidence,
        Hash256 Topology,
        Hash256 Impact,
        IReadOnlyList<Hash256> Devices,
        Hash256 Fingerprint);
}

/// <summary>Insert-only warning acknowledgment of an exact hash.</summary>
public sealed class AcknowledgeWarningUseCase
{
    public const string Operation = "policy.acknowledge_warning";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public AcknowledgeWarningUseCase(
        IAuthorizationBoundary auth,
        IPolicyApprovalStore approvals,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _approvals = approvals;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<PolicyAnalysisRunView>> ExecuteAsync(
        AcknowledgeWarningCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyApprove, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        ApplicationError? hashError = PolicyRevisionSupport.TryHash(command.WarningHash, "warning_hash", out Hash256? warningHash);
        if (hashError is not null)
        {
            return ApplicationResults.Fail(hashError);
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.AnalysisRunId,
            warning_hash = warningHash!.ToString(),
        });
        ApplicationResult<PolicyAnalysisRunView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (runId, ct) =>
            {
                PolicyAnalysisRun? existing = await _approvals
                    .GetAnalysisRunAsync(new PolicyAnalysisRunId(runId), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Analysis run '{runId}' not found."))
                    : ApplicationResults.Ok(ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        PolicyAnalysisRun? run = await _approvals
            .GetAnalysisRunAsync(new PolicyAnalysisRunId(command.AnalysisRunId), cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Analysis run '{command.AnalysisRunId}' not found."));
        }

        if (!run.Findings.Any(f => f.WarningHash.Equals(warningHash)))
        {
            return ApplicationResults.Fail(new ApplicationError(
                PolicyApprovalCodes.WarningUnacked,
                "Warning hash is not present on the analysis run."));
        }

        PolicyWarningAcknowledgment ack = PolicyWarningAcknowledgment.Create(
            run.Id,
            warningHash!,
            new UserId(ActorKey.FromActor(command.Actor)),
            DateTimeOffset.UtcNow);
        await _approvals.AddWarningAcknowledgmentAsync(ack, cancellationToken).ConfigureAwait(false);
        await _idempotency.SaveAsync(
            command.Actor, Operation, command.IdempotencyKey, requestHash, run.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new
            {
                analysis_run_id = run.Id.Value,
                warning_hash = warningHash!.ToString(),
            }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(ToView(run));
    }

    private static PolicyAnalysisRunView ToView(PolicyAnalysisRun run)
        => new()
        {
            Id = run.Id.Value,
            RevisionId = run.RevisionId.Value,
            BundleHashHex = run.BundleHash.ToString(),
            DependencyFingerprintHex = run.DependencyFingerprint.ToString(),
            RiskLevel = run.RiskLevel,
            EffectiveRiskLevel = run.EffectiveRiskLevel(),
            EvidenceSignalsPresent = run.EvidenceSignalsPresent,
        };
}

/// <summary>VALIDATED → IN_REVIEW with CAS and audit.</summary>
public sealed class SubmitRevisionForReviewUseCase
{
    public const string Operation = "policy.submit_for_review";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public SubmitRevisionForReviewUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _policies = policies;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        SubmitRevisionForReviewCommand command,
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
            revision!.SubmitForReview();
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(new ApplicationError(PolicyApprovalCodes.NotInReview, ex.Message));
        }

        await _policies.SaveRevisionAsync(revision, cancellationToken).ConfigureAwait(false);
        await _idempotency.SaveAsync(
            command.Actor, Operation, command.IdempotencyKey, requestHash, revision.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new { revision_id = revision.Id.Value, state = revision.State.ToString() }),
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

/// <summary>Approval vote. Never activates a desired binding.</summary>
public sealed class ApproveRevisionUseCase
{
    public const string Operation = "policy.approve_revision";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveRevisionUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _approvals = approvals;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyApprovalVoteView>> ExecuteAsync(
        ApproveRevisionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyApprove, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? securityAuth = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyApproveSecurity, cancellationToken)
            .ConfigureAwait(false);
        bool isSecurityOwner = securityAuth is null;

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
            bundle_hash = Convert.ToHexString(command.ExpectedBundleHash).ToLowerInvariant(),
            fingerprint = Convert.ToHexString(command.CurrentDependencyFingerprint).ToLowerInvariant(),
        });
        ApplicationResult<PolicyApprovalVoteView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            LoadVoteViewAsync,
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

        ApplicationError? bundleErr = PolicyRevisionSupport.TryHash(
            command.ExpectedBundleHash, "expected_bundle_hash", out Hash256? bundle);
        if (bundleErr is not null)
        {
            return ApplicationResults.Fail(bundleErr);
        }

        ApplicationError? fpErr = PolicyRevisionSupport.TryHash(
            command.CurrentDependencyFingerprint, "current_dependency_fingerprint", out Hash256? fingerprint);
        if (fpErr is not null)
        {
            return ApplicationResults.Fail(fpErr);
        }

        IReadOnlyList<PolicyWarningAcknowledgment> acks = await _approvals
            .ListAcknowledgmentsAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PolicyApproval> votes = await _approvals
            .ListApprovalsAsync(revision.Id, cancellationToken)
            .ConfigureAwait(false);
        UserId reviewer = new(ActorKey.FromActor(command.Actor));
        PolicyApproval? existingVote = votes.FirstOrDefault(
            v => v.ReviewerId == reviewer && v.AnalysisRunId == run.Id);
        if (existingVote is not null)
        {
            return await CompleteExistingVoteAsync(
                command,
                requestHash,
                revision,
                policy,
                run,
                bundle!,
                fingerprint!,
                acks,
                votes,
                existingVote,
                reviewer,
                isSecurityOwner,
                cancellationToken).ConfigureAwait(false);
        }

        PolicyApprovalEvaluation evaluation = PolicyApprovalGate.Evaluate(
            revision,
            policy,
            run,
            bundle!,
            fingerprint!,
            acks,
            votes,
            reviewer,
            isSecurityOwner);
        if (evaluation.Outcome == PolicyApprovalCodes.OutcomeReject)
        {
            return ApplicationResults.Fail(new ApplicationError(
                evaluation.ErrorCode ?? PolicyApprovalCodes.Blocker,
                evaluation.ErrorMessage ?? "Approval rejected."));
        }

        PolicyApproval vote = PolicyApproval.Create(
            revision.Id,
            run.Id,
            run.BundleHash,
            reviewer,
            isSecurityOwner,
            DateTimeOffset.UtcNow);
        if (evaluation.CompletesApproval)
        {
            try
            {
                revision.Approve(DateTimeOffset.UtcNow, run.Id, run.BundleHash);
            }
            catch (DomainInvariantException ex)
            {
                return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
            }
        }

        try
        {
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _approvals.AddApprovalAsync(vote, ct).ConfigureAwait(false);
                    if (evaluation.CompletesApproval)
                    {
                        await _policies.SaveRevisionAsync(revision, ct).ConfigureAwait(false);
                    }

                    await _idempotency.SaveAsync(
                        command.Actor, Operation, command.IdempotencyKey, requestHash, vote.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                        command.Actor,
                        Operation,
                        JsonSerializer.Serialize(new
                        {
                            revision_id = revision.Id.Value,
                            analysis_run_id = run.Id.Value,
                            approval_id = vote.Id.Value,
                            completes_approval = evaluation.CompletesApproval,
                            state = revision.State.ToString(),
                            binding_activated = false,
                        }),
                        ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (PersistenceConflictException ex)
        {
            return ApplicationResults.Fail(new ApplicationError(ex.Code, ex.Message));
        }

        return ApplicationResults.Ok(ToVoteView(vote, revision, evaluation.CompletesApproval));
    }

    private async Task<ApplicationResult<PolicyApprovalVoteView>> CompleteExistingVoteAsync(
        ApproveRevisionCommand command,
        byte[] requestHash,
        PolicyRevision revision,
        Policy policy,
        PolicyAnalysisRun run,
        Hash256 bundle,
        Hash256 fingerprint,
        IReadOnlyList<PolicyWarningAcknowledgment> acks,
        IReadOnlyList<PolicyApproval> votes,
        PolicyApproval existingVote,
        UserId reviewer,
        bool isSecurityOwner,
        CancellationToken cancellationToken)
    {
        bool completes = revision.State == PolicyRevisionState.Approved;
        bool persistApproval = false;
        if (revision.State == PolicyRevisionState.InReview)
        {
            PolicyApproval[] others = votes
                .Where(v => v.ReviewerId != reviewer)
                .ToArray();
            PolicyApprovalEvaluation evaluation = PolicyApprovalGate.Evaluate(
                revision,
                policy,
                run,
                bundle,
                fingerprint,
                acks,
                others,
                reviewer,
                isSecurityOwner);
            if (evaluation.Outcome == PolicyApprovalCodes.OutcomeReject)
            {
                return ApplicationResults.Fail(new ApplicationError(
                    evaluation.ErrorCode ?? PolicyApprovalCodes.Blocker,
                    evaluation.ErrorMessage ?? "Approval rejected."));
            }

            if (evaluation.CompletesApproval)
            {
                try
                {
                    revision.Approve(DateTimeOffset.UtcNow, run.Id, run.BundleHash);
                }
                catch (DomainInvariantException ex)
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
                }

                completes = true;
                persistApproval = true;
            }
        }

        try
        {
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    if (persistApproval)
                    {
                        await _policies.SaveRevisionAsync(revision, ct).ConfigureAwait(false);
                    }

                    await _idempotency.SaveAsync(
                        command.Actor, Operation, command.IdempotencyKey, requestHash, existingVote.Id.Value, ct)
                        .ConfigureAwait(false);
                    if (persistApproval)
                    {
                        await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new
                            {
                                revision_id = revision.Id.Value,
                                analysis_run_id = run.Id.Value,
                                approval_id = existingVote.Id.Value,
                                completes_approval = true,
                                state = revision.State.ToString(),
                                binding_activated = false,
                            }),
                            ct).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (PersistenceConflictException ex)
        {
            return ApplicationResults.Fail(new ApplicationError(ex.Code, ex.Message));
        }

        return ApplicationResults.Ok(ToVoteView(
            existingVote,
            revision,
            completes || revision.State == PolicyRevisionState.Approved));
    }

    private async Task<ApplicationResult<PolicyApprovalVoteView>> LoadVoteViewAsync(
        Guid approvalId,
        CancellationToken cancellationToken)
    {
        PolicyApproval? vote = await _approvals
            .GetApprovalAsync(new PolicyApprovalId(approvalId), cancellationToken)
            .ConfigureAwait(false);
        if (vote is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Approval vote '{approvalId}' not found."));
        }

        PolicyRevision? revision = await _policies
            .GetRevisionAsync(vote.RevisionId, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy revision '{vote.RevisionId}' not found."));
        }

        return ApplicationResults.Ok(ToVoteView(
            vote,
            revision,
            revision.State == PolicyRevisionState.Approved));
    }

    private static PolicyApprovalVoteView ToVoteView(PolicyApproval vote, PolicyRevision revision, bool completes)
        => new()
        {
            ApprovalId = vote.Id.Value,
            RevisionId = revision.Id.Value,
            RevisionState = revision.State,
            CompletesApproval = completes,
            BundleHashHex = vote.BundleHash.ToString(),
            BindingIds = [],
        };
}
