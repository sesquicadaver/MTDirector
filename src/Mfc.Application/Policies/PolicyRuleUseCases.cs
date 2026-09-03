using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

public sealed class CreateDraftPolicyCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required string Name { get; init; }

    public required PolicyKind Kind { get; init; }

    public required PolicyOwnerScope OwnerScope { get; init; }

    public Guid? OwnerId { get; init; }

    /// <summary>Required for non-COMPANY_BASELINE kinds (32-byte SHA-256).</summary>
    public byte[]? ParentContextHash { get; init; }
}

public sealed class CreateDraftPolicyUseCase
{
    public const string Operation = "policy.create_draft";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDraftPolicyUseCase(
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

    public async Task<ApplicationResult<PolicyDraftView>> ExecuteAsync(
        CreateDraftPolicyCommand command,
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
            command.Name,
            kind = command.Kind.ToString(),
            owner_scope = command.OwnerScope.ToString(),
            command.OwnerId,
            parent_context_hash = command.ParentContextHash is null
                ? null
                : Convert.ToHexString(command.ParentContextHash).ToLowerInvariant(),
        });
        ApplicationResult<PolicyDraftView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                Policy? existing = await _policies.GetPolicyAsync(new PolicyId(id), ct).ConfigureAwait(false);
                if (existing is null)
                {
                    return ApplicationResults.Fail(ApplicationError.NotFound($"Policy '{id}' not found."));
                }

                IReadOnlyList<PolicyRevision> revisions = await _policies.ListRevisionsAsync(existing.Id, ct)
                    .ConfigureAwait(false);
                PolicyRevision? first = revisions.OrderBy(r => r.RevisionNumber).FirstOrDefault();
                if (first is null)
                {
                    return ApplicationResults.Fail(
                        ApplicationError.NotFound($"Policy '{id}' has no revisions."));
                }

                return ApplicationResults.Ok(ToDraftView(existing, first));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            Hash256? parentContext = null;
            if (command.ParentContextHash is not null)
            {
                if (command.ParentContextHash.Length != Hash256.Size)
                {
                    return ApplicationResults.Fail(
                        ApplicationError.Validation(
                            $"parent_context_hash must be exactly {Hash256.Size} bytes (SHA-256)."));
                }

                parentContext = Hash256.Create(command.ParentContextHash);
            }

            Policy policy = Policy.Create(
                NonEmptyName.Create(command.Name),
                command.Kind,
                command.OwnerScope,
                command.OwnerId);
            PolicyDocument document = PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope);
            PolicyRevision revision = PolicyRevision.CreateDraft(
                policy,
                revisionNumber: 1,
                document,
                parentContext,
                new UserId(ActorKey.FromActor(command.Actor)),
                DateTimeOffset.UtcNow);

            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _policies.AddPolicyAsync(policy, ct).ConfigureAwait(false);
                    await _policies.AddRevisionAsync(revision, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                            command.Actor, Operation, command.IdempotencyKey, requestHash, policy.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new
                            {
                                policy_id = policy.Id.Value,
                                revision_id = revision.Id.Value,
                                content_hash = revision.ContentHash.ToString(),
                            }),
                            ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ToDraftView(policy, revision));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }

    private static PolicyDraftView ToDraftView(Policy policy, PolicyRevision revision) => new()
    {
        PolicyId = policy.Id.Value,
        RevisionId = revision.Id.Value,
        Name = policy.Name.Value,
        Kind = policy.Kind,
        OwnerScope = policy.OwnerScope,
        OwnerId = policy.OwnerId,
        RevisionNumber = revision.RevisionNumber,
        ContentHashHex = revision.ContentHash.ToString(),
    };
}

public sealed class GetPolicyRevisionQuery
{
    public required string Actor { get; init; }

    public required Guid RevisionId { get; init; }
}

public sealed class GetPolicyRevisionUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;

    public GetPolicyRevisionUseCase(IAuthorizationBoundary auth, IPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        _auth = auth;
        _policies = policies;
    }

    public async Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        GetPolicyRevisionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(_policies, query.RevisionId, cancellationToken)
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

public sealed class ListRulesQuery
{
    public required string Actor { get; init; }

    public required Guid RevisionId { get; init; }

    /// <summary>When false, returns all rules including disabled (default). When true, active only.</summary>
    public bool ActiveOnly { get; init; }
}

public sealed class ListRulesUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;

    public ListRulesUseCase(IAuthorizationBoundary auth, IPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        _auth = auth;
        _policies = policies;
    }

    public async Task<ApplicationResult<PolicyRuleListView>> ExecuteAsync(
        ListRulesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(_policies, query.RevisionId, cancellationToken)
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

        IReadOnlyList<PolicyRule> source = query.ActiveOnly
            ? PolicyRuleSet.ActiveRules(document.Value!.Rules)
            : document.Value!.Rules;
        PolicyRuleView[] rules = source.Select(r => ViewMapper.ToView(r, document.Value!)).ToArray();
        return ApplicationResults.Ok(new PolicyRuleListView
        {
            RevisionId = revision!.Id.Value,
            ContentHashHex = revision.ContentHash.ToString(),
            Rules = rules,
            Warnings = PolicyRevisionSupport.MergeWarnings(rules),
        });
    }
}

public sealed class GetRuleQuery
{
    public required string Actor { get; init; }

    public required Guid RevisionId { get; init; }

    public required Guid RuleId { get; init; }
}

public sealed class GetRuleUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;

    public GetRuleUseCase(IAuthorizationBoundary auth, IPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        _auth = auth;
        _policies = policies;
    }

    public async Task<ApplicationResult<PolicyRuleView>> ExecuteAsync(
        GetRuleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(_policies, query.RevisionId, cancellationToken)
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

        PolicyRule? rule = document.Value!.Rules.FirstOrDefault(r => r.Id.Value == query.RuleId);
        if (rule is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Rule '{query.RuleId}' not found."));
        }

        return ApplicationResults.Ok(ViewMapper.ToView(rule, document.Value!));
    }
}

public sealed class AddRuleCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required PolicyPipelineStage Stage { get; init; }

    public uint Ordinal { get; init; }

    public bool Enabled { get; init; } = true;

    public TrafficPredicateInput? Predicate { get; init; }

    public required RuleEffectInput Effect { get; init; }

    public LogSpecificationInput? Logging { get; init; }

    public bool ExceptionEligible { get; init; }

    public string? Description { get; init; }
}

public sealed class AddRuleUseCase
{
    public const string Operation = "policy.add_rule";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IZoneDefinitionStore _zones;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public AddRuleUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _zones = zones;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyRuleMutationView>> ExecuteAsync(
        AddRuleCommand command,
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
            family = command.Family.ToString(),
            chain = command.Chain.ToString(),
            stage = command.Stage.ToString(),
            command.Ordinal,
            command.Enabled,
            command.ExceptionEligible,
            command.Description,
            effect = command.Effect.Kind.ToString(),
            reject_mode = command.Effect.RejectMode?.ToString(),
            predicate = PolicyRuleIdempotency.HashPredicate(command.Predicate),
            logging = PolicyRuleIdempotency.HashLogging(command.Logging),
        });
        ApplicationResult<PolicyRuleMutationView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (ruleId, ct) => await LoadMutationByRuleIdAsync(command.RevisionId, ruleId, ct)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        return await MutateAsync(
            command.RevisionId,
            command.ExpectedContentHash,
            document =>
            {
                PolicyRule rule = PolicyRuleFactory.CreateRule(
                    command.Family,
                    command.Chain,
                    command.Stage,
                    command.Ordinal,
                    command.Predicate,
                    command.Effect,
                    command.Logging,
                    command.Enabled,
                    command.ExceptionEligible,
                    command.Description);
                IReadOnlyList<PolicyRule> next = PolicyRuleSet.WithAdd(document.Rules, rule);
                return (document.WithRules(next), rule.Id.Value, rule.Id);
            },
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApplicationResult<PolicyRuleMutationView>> LoadMutationByRuleIdAsync(
        Guid revisionId,
        Guid ruleId,
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

        PolicyRuleView[] rules = document.Value!.Rules.Select(r => ViewMapper.ToView(r, document.Value!)).ToArray();
        PolicyRuleView? rule = rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Rule '{ruleId}' not found."));
        }

        return ApplicationResults.Ok(new PolicyRuleMutationView
        {
            ContentHashHex = revision!.ContentHash.ToString(),
            Rule = rule,
            Rules = rules,
            Warnings = PolicyRevisionSupport.MergeWarnings(rules),
        });
    }

    private Task<ApplicationResult<PolicyRuleMutationView>> MutateAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        Func<PolicyDocument, (PolicyDocument Next, Guid ResourceId, RuleId FocusRuleId)> mutate,
        string actor,
        string operation,
        Guid idempotencyKey,
        byte[] requestHash,
        CancellationToken cancellationToken)
        => PolicyRuleMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _zones,
            _idempotency,
            _audit,
            _unitOfWork,
            revisionId,
            expectedContentHash,
            mutate,
            actor,
            operation,
            idempotencyKey,
            requestHash,
            cancellationToken);
}

public sealed class UpdateRuleCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required Guid RuleId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required PolicyPipelineStage Stage { get; init; }

    public uint Ordinal { get; init; }

    public bool Enabled { get; init; } = true;

    public TrafficPredicateInput? Predicate { get; init; }

    public required RuleEffectInput Effect { get; init; }

    public LogSpecificationInput? Logging { get; init; }

    public bool ExceptionEligible { get; init; }

    public string? Description { get; init; }
}

public sealed class UpdateRuleUseCase
{
    public const string Operation = "policy.update_rule";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IZoneDefinitionStore _zones;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRuleUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _zones = zones;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyRuleMutationView>> ExecuteAsync(
        UpdateRuleCommand command,
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
            command.RuleId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
            family = command.Family.ToString(),
            chain = command.Chain.ToString(),
            stage = command.Stage.ToString(),
            command.Ordinal,
            command.Enabled,
            command.ExceptionEligible,
            command.Description,
            effect = command.Effect.Kind.ToString(),
            reject_mode = command.Effect.RejectMode?.ToString(),
            predicate = PolicyRuleIdempotency.HashPredicate(command.Predicate),
            logging = PolicyRuleIdempotency.HashLogging(command.Logging),
        });
        ApplicationResult<PolicyRuleMutationView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (ruleId, ct) =>
            {
                (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
                    .LoadRevisionAsync(_policies, command.RevisionId, ct)
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

                PolicyRuleView[] rules = document.Value!.Rules.Select(r => ViewMapper.ToView(r, document.Value!)).ToArray();
                PolicyRuleView? rule = rules.FirstOrDefault(r => r.Id == ruleId);
                if (rule is null)
                {
                    return ApplicationResults.Fail(ApplicationError.NotFound($"Rule '{ruleId}' not found."));
                }

                return ApplicationResults.Ok(new PolicyRuleMutationView
                {
                    ContentHashHex = revision!.ContentHash.ToString(),
                    Rule = rule,
                    Rules = rules,
                    Warnings = PolicyRevisionSupport.MergeWarnings(rules),
                });
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        return await PolicyRuleMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _zones,
            _idempotency,
            _audit,
            _unitOfWork,
            command.RevisionId,
            command.ExpectedContentHash,
            document =>
            {
                if (document.Rules.All(r => r.Id.Value != command.RuleId))
                {
                    throw new DomainInvariantException($"Rule id '{command.RuleId}' was not found.");
                }

                PolicyRule updated = PolicyRuleFactory.CreateRule(
                    command.Family,
                    command.Chain,
                    command.Stage,
                    command.Ordinal,
                    command.Predicate,
                    command.Effect,
                    command.Logging,
                    command.Enabled,
                    command.ExceptionEligible,
                    command.Description,
                    new RuleId(command.RuleId));
                IReadOnlyList<PolicyRule> next = PolicyRuleSet.WithUpdate(document.Rules, updated);
                return (document.WithRules(next), command.RuleId, updated.Id);
            },
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DeleteRuleCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required Guid RuleId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }
}

public sealed class DeleteRuleUseCase
{
    public const string Operation = "policy.delete_rule";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IZoneDefinitionStore _zones;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRuleUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _zones = zones;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyRuleMutationView>> ExecuteAsync(
        DeleteRuleCommand command,
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
            command.RuleId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
        });
        ApplicationResult<PolicyRuleMutationView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (_, ct) =>
            {
                (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
                    .LoadRevisionAsync(_policies, command.RevisionId, ct)
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

                PolicyRuleView[] rules = document.Value!.Rules.Select(r => ViewMapper.ToView(r, document.Value!)).ToArray();
                return ApplicationResults.Ok(new PolicyRuleMutationView
                {
                    ContentHashHex = revision!.ContentHash.ToString(),
                    Rule = null,
                    Rules = rules,
                    Warnings = PolicyRevisionSupport.MergeWarnings(rules),
                });
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        return await PolicyRuleMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _zones,
            _idempotency,
            _audit,
            _unitOfWork,
            command.RevisionId,
            command.ExpectedContentHash,
            document =>
            {
                IReadOnlyList<PolicyRule> next = PolicyRuleSet.WithDelete(document.Rules, new RuleId(command.RuleId));
                return (document.WithRules(next), command.RuleId, new RuleId(command.RuleId));
            },
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ReorderRulesCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required PolicyPipelineStage Stage { get; init; }

    public required IReadOnlyList<Guid> OrderedRuleIds { get; init; }
}

public sealed class ReorderRulesUseCase
{
    public const string Operation = "policy.reorder_rules";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IZoneDefinitionStore _zones;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderRulesUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _zones = zones;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyRuleMutationView>> ExecuteAsync(
        ReorderRulesCommand command,
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
            family = command.Family.ToString(),
            chain = command.Chain.ToString(),
            stage = command.Stage.ToString(),
            ordered_rule_ids = command.OrderedRuleIds,
        });
        ApplicationResult<PolicyRuleMutationView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (_, ct) =>
            {
                (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
                    .LoadRevisionAsync(_policies, command.RevisionId, ct)
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

                PolicyRuleView[] rules = document.Value!.Rules.Select(r => ViewMapper.ToView(r, document.Value!)).ToArray();
                return ApplicationResults.Ok(new PolicyRuleMutationView
                {
                    ContentHashHex = revision!.ContentHash.ToString(),
                    Rule = null,
                    Rules = rules,
                    Warnings = PolicyRevisionSupport.MergeWarnings(rules),
                });
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        return await PolicyRuleMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _zones,
            _idempotency,
            _audit,
            _unitOfWork,
            command.RevisionId,
            command.ExpectedContentHash,
            document =>
            {
                IReadOnlyList<PolicyRule> next = PolicyRuleSet.WithReorder(
                    document.Rules,
                    command.Family,
                    command.Chain,
                    command.Stage,
                    command.OrderedRuleIds.Select(static id => new RuleId(id)).ToArray());
                return (document.WithRules(next), command.RevisionId, new RuleId(command.RevisionId));
            },
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Shared draft mutate → CAS → catalog → ReplaceDocument → SaveRevision pipeline.</summary>
internal static class PolicyRuleMutationPipeline
{
    public static async Task<ApplicationResult<PolicyRuleMutationView>> ExecuteAsync(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork,
        Guid revisionId,
        byte[] expectedContentHash,
        Func<PolicyDocument, (PolicyDocument Next, Guid ResourceId, RuleId FocusRuleId)> mutate,
        string actor,
        string operation,
        Guid idempotencyKey,
        byte[] requestHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _ = auth;
        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(policies, revisionId, cancellationToken)
            .ConfigureAwait(false);
        if (loadError is not null)
        {
            return ApplicationResults.Fail(loadError);
        }

        ApplicationError? editable = PolicyRevisionSupport.EnsureEditable(revision!);
        if (editable is not null)
        {
            return ApplicationResults.Fail(editable);
        }

        ApplicationError? cas = PolicyRevisionSupport.EnsureContentHash(revision!, expectedContentHash);
        if (cas is not null)
        {
            return ApplicationResults.Fail(cas);
        }

        ApplicationResult<PolicyDocument> documentResult = PolicyRevisionSupport.ReadDocument(revision!);
        if (documentResult.IsFailure)
        {
            return ApplicationResults.Fail(documentResult.Error!);
        }

        try
        {
            (PolicyDocument next, Guid resourceId, RuleId focusRuleId) = mutate(documentResult.Value!);
            _ = focusRuleId;
            IEnumerable<PolicyRule> toValidate = next.Rules.Any(r => r.Id.Value == resourceId)
                ? next.Rules.Where(r => r.Id.Value == resourceId)
                : next.Rules;
            foreach (PolicyRule rule in toValidate)
            {
                ApplicationError? zoneError = await PolicyRevisionSupport
                    .EnsureZonesExistAsync(zones, rule.Predicate, cancellationToken)
                    .ConfigureAwait(false);
                if (zoneError is not null)
                {
                    return ApplicationResults.Fail(zoneError);
                }

                ApplicationError? catalogError = PolicyRevisionSupport.EnsureAddressServiceCatalog(
                    next, rule.Predicate);
                if (catalogError is not null)
                {
                    return ApplicationResults.Fail(catalogError);
                }
            }

            revision!.ReplaceDocument(next, revision.ParentContextHash);
            await unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await policies.SaveRevisionAsync(revision, ct).ConfigureAwait(false);
                    await idempotency.SaveAsync(
                            actor, operation, idempotencyKey, requestHash, resourceId, ct)
                        .ConfigureAwait(false);
                    await audit.AppendAsync(
                            actor,
                            operation,
                            JsonSerializer.Serialize(new
                            {
                                revision_id = revision.Id.Value,
                                content_hash = revision.ContentHash.ToString(),
                                resource_id = resourceId,
                            }),
                            ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            PolicyRuleView[] rules = next.Rules.Select(r => ViewMapper.ToView(r, next)).ToArray();
            PolicyRuleView? ruleView = rules.FirstOrDefault(r => r.Id == resourceId);
            return ApplicationResults.Ok(new PolicyRuleMutationView
            {
                ContentHashHex = revision.ContentHash.ToString(),
                Rule = ruleView,
                Rules = rules,
                Warnings = PolicyRevisionSupport.MergeWarnings(rules),
            });
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}
