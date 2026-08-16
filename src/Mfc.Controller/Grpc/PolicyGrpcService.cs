using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for PolicyService (draft/rule CRUD + compose + exception metadata + approval/binding).</summary>
public sealed class PolicyGrpcService : PolicyService.PolicyServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly CreateDraftPolicyUseCase _createDraft;
    private readonly GetPolicyRevisionUseCase _getRevision;
    private readonly ListRulesUseCase _listRules;
    private readonly GetRuleUseCase _getRule;
    private readonly AddRuleUseCase _addRule;
    private readonly UpdateRuleUseCase _updateRule;
    private readonly DeleteRuleUseCase _deleteRule;
    private readonly ReorderRulesUseCase _reorderRules;
    private readonly ComposeEffectivePolicyUseCase _composeEffective;
    private readonly UpdateExceptionMetadataUseCase _updateExceptionMetadata;
    private readonly RecordAnalysisRunUseCase _recordAnalysisRun;
    private readonly AcknowledgeWarningUseCase _acknowledgeWarning;
    private readonly SubmitRevisionForReviewUseCase _submitForReview;
    private readonly ApproveRevisionUseCase _approveRevision;
    private readonly ActivateDesiredBindingUseCase _activateBinding;
    private readonly ExpireExceptionBindingUseCase _expireException;
    private readonly IHostEnvironment _environment;

    public PolicyGrpcService(
        CreateDraftPolicyUseCase createDraft,
        GetPolicyRevisionUseCase getRevision,
        ListRulesUseCase listRules,
        GetRuleUseCase getRule,
        AddRuleUseCase addRule,
        UpdateRuleUseCase updateRule,
        DeleteRuleUseCase deleteRule,
        ReorderRulesUseCase reorderRules,
        ComposeEffectivePolicyUseCase composeEffective,
        UpdateExceptionMetadataUseCase updateExceptionMetadata,
        RecordAnalysisRunUseCase recordAnalysisRun,
        AcknowledgeWarningUseCase acknowledgeWarning,
        SubmitRevisionForReviewUseCase submitForReview,
        ApproveRevisionUseCase approveRevision,
        ActivateDesiredBindingUseCase activateBinding,
        ExpireExceptionBindingUseCase expireException,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(createDraft);
        ArgumentNullException.ThrowIfNull(getRevision);
        ArgumentNullException.ThrowIfNull(listRules);
        ArgumentNullException.ThrowIfNull(getRule);
        ArgumentNullException.ThrowIfNull(addRule);
        ArgumentNullException.ThrowIfNull(updateRule);
        ArgumentNullException.ThrowIfNull(deleteRule);
        ArgumentNullException.ThrowIfNull(reorderRules);
        ArgumentNullException.ThrowIfNull(composeEffective);
        ArgumentNullException.ThrowIfNull(updateExceptionMetadata);
        ArgumentNullException.ThrowIfNull(recordAnalysisRun);
        ArgumentNullException.ThrowIfNull(acknowledgeWarning);
        ArgumentNullException.ThrowIfNull(submitForReview);
        ArgumentNullException.ThrowIfNull(approveRevision);
        ArgumentNullException.ThrowIfNull(activateBinding);
        ArgumentNullException.ThrowIfNull(expireException);
        ArgumentNullException.ThrowIfNull(environment);
        _createDraft = createDraft;
        _getRevision = getRevision;
        _listRules = listRules;
        _getRule = getRule;
        _addRule = addRule;
        _updateRule = updateRule;
        _deleteRule = deleteRule;
        _reorderRules = reorderRules;
        _composeEffective = composeEffective;
        _updateExceptionMetadata = updateExceptionMetadata;
        _recordAnalysisRun = recordAnalysisRun;
        _acknowledgeWarning = acknowledgeWarning;
        _submitForReview = submitForReview;
        _approveRevision = approveRevision;
        _activateBinding = activateBinding;
        _expireException = expireException;
        _environment = environment;
    }

    public override async Task<PolicyDraft> CreateDraftPolicy(
        CreateDraftPolicyRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyDraftView> result = await _createDraft.ExecuteAsync(
            new CreateDraftPolicyCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                Name = request.Name,
                Kind = PolicyProtoMapper.ToDomain(request.Kind),
                OwnerScope = PolicyProtoMapper.ToDomain(request.OwnerScope),
                OwnerId = request.OwnerId is null ? null : ProtoUuid.ToGuid(request.OwnerId),
                ParentContextHash = PolicyProtoMapper.ToOptionalHashBytes(request.ParentContextHash),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyRevision> GetPolicyRevision(
        GetPolicyRevisionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRevisionView> result = await _getRevision.ExecuteAsync(
            new GetPolicyRevisionQuery
            {
                Actor = ResolveActor(context),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ListRulesResponse> ListRules(
        ListRulesRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRuleListView> result = await _listRules.ExecuteAsync(
            new ListRulesQuery
            {
                Actor = ResolveActor(context),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                ActiveOnly = request.ActiveOnly,
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyRule> GetRule(
        GetRuleRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRuleView> result = await _getRule.ExecuteAsync(
            new GetRuleQuery
            {
                Actor = ResolveActor(context),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                RuleId = ProtoUuid.ToGuid(request.RuleId),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<PolicyRuleMutation> AddRule(
        AddRuleRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRuleMutationView> result = await _addRule.ExecuteAsync(
            new AddRuleCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                Family = PolicyProtoMapper.ToDomain(request.Family),
                Chain = PolicyProtoMapper.ToDomain(request.Chain),
                Stage = PolicyProtoMapper.ToDomain(request.Stage),
                Ordinal = request.Ordinal,
                Enabled = request.Enabled,
                Predicate = PolicyProtoMapper.ToInput(request.Predicate),
                Effect = PolicyProtoMapper.ToInput(request.Effect),
                Logging = PolicyProtoMapper.ToInput(request.Logging),
                ExceptionEligible = request.ExceptionEligible,
                Description = request.Description,
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<PolicyRuleMutation> UpdateRule(
        UpdateRuleRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRuleMutationView> result = await _updateRule.ExecuteAsync(
            new UpdateRuleCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                RuleId = ProtoUuid.ToGuid(request.RuleId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                Family = PolicyProtoMapper.ToDomain(request.Family),
                Chain = PolicyProtoMapper.ToDomain(request.Chain),
                Stage = PolicyProtoMapper.ToDomain(request.Stage),
                Ordinal = request.Ordinal,
                Enabled = request.Enabled,
                Predicate = PolicyProtoMapper.ToInput(request.Predicate),
                Effect = PolicyProtoMapper.ToInput(request.Effect),
                Logging = PolicyProtoMapper.ToInput(request.Logging),
                ExceptionEligible = request.ExceptionEligible,
                Description = request.Description,
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<PolicyRuleMutation> DeleteRule(
        DeleteRuleRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRuleMutationView> result = await _deleteRule.ExecuteAsync(
            new DeleteRuleCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                RuleId = ProtoUuid.ToGuid(request.RuleId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<PolicyRuleMutation> ReorderRules(
        ReorderRulesRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRuleMutationView> result = await _reorderRules.ExecuteAsync(
            new ReorderRulesCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                Family = PolicyProtoMapper.ToDomain(request.Family),
                Chain = PolicyProtoMapper.ToDomain(request.Chain),
                Stage = PolicyProtoMapper.ToDomain(request.Stage),
                OrderedRuleIds = request.OrderedRuleIds.Select(ProtoUuid.ToGuid).ToArray(),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<EffectivePolicy> ComposeEffectivePolicy(
        ComposeEffectivePolicyRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<EffectivePolicyView> result = await _composeEffective.ExecuteAsync(
            new ComposeEffectivePolicyQuery
            {
                Actor = ResolveActor(context),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyRevision> UpdateExceptionMetadata(
        UpdateExceptionMetadataRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Metadata is null)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(
                ApplicationError.Validation("exception_metadata is required."));
        }

        ExceptionMetadataInput metadata;
        try
        {
            metadata = PolicyProtoMapper.ToInput(request.Metadata);
        }
        catch (DomainInvariantException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }

        ApplicationResult<PolicyRevisionView> result = await _updateExceptionMetadata.ExecuteAsync(
            new UpdateExceptionMetadataCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                Metadata = metadata,
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyAnalysisRun> RecordAnalysisRun(
        RecordAnalysisRunRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyAnalysisRunView> result = await _recordAnalysisRun.ExecuteAsync(
            new RecordAnalysisRunCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                LogicalEffectiveHash = PolicyProtoMapper.ToHashBytes(request.LogicalEffectiveHash),
                AnalysisContextHash = PolicyProtoMapper.ToHashBytes(request.AnalysisContextHash),
                EvidenceContextHash = PolicyProtoMapper.ToHashBytes(request.EvidenceContextHash),
                TopologyProjectionHash = PolicyProtoMapper.ToHashBytes(request.TopologyProjectionHash),
                ImpactSetHash = PolicyProtoMapper.ToHashBytes(request.ImpactSetHash),
                PerDeviceAnalysisHashes = request.PerDeviceAnalysisHashes.Select(PolicyProtoMapper.ToHashBytes).ToArray(),
                DependencyFingerprint = PolicyProtoMapper.ToHashBytes(request.DependencyFingerprint),
                RiskLevel = request.RiskLevel,
                EvidenceSignalsPresent = request.EvidenceSignalsPresent,
                AnalyzerVersion = request.AnalyzerVersion,
                PolicySchemaVersion = request.PolicySchemaVersion,
                PipelineVersion = request.PipelineVersion,
                Findings = request.Findings.Select(PolicyProtoMapper.ToInput).ToArray(),
                TestResults = request.TestResults.Select(PolicyProtoMapper.ToInput).ToArray(),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyAnalysisRun> AcknowledgeWarning(
        AcknowledgeWarningRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyAnalysisRunView> result = await _acknowledgeWarning.ExecuteAsync(
            new AcknowledgeWarningCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                AnalysisRunId = ProtoUuid.ToGuid(request.AnalysisRunId),
                WarningHash = PolicyProtoMapper.ToHashBytes(request.WarningHash),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyRevision> SubmitRevisionForReview(
        SubmitRevisionForReviewRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyRevisionView> result = await _submitForReview.ExecuteAsync(
            new SubmitRevisionForReviewCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<PolicyApprovalVote> ApproveRevision(
        ApproveRevisionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyApprovalVoteView> result = await _approveRevision.ExecuteAsync(
            new ApproveRevisionCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                AnalysisRunId = ProtoUuid.ToGuid(request.AnalysisRunId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                ExpectedBundleHash = PolicyProtoMapper.ToHashBytes(request.ExpectedBundleHash),
                CurrentDependencyFingerprint = PolicyProtoMapper.ToHashBytes(request.CurrentDependencyFingerprint),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyBinding> ActivateDesiredBinding(
        ActivateDesiredBindingRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyBindingView> result = await _activateBinding.ExecuteAsync(
            new ActivateDesiredBindingCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                RevisionId = ProtoUuid.ToGuid(request.RevisionId),
                AnalysisRunId = ProtoUuid.ToGuid(request.AnalysisRunId),
                ExpectedContentHash = PolicyProtoMapper.ToHashBytes(request.ExpectedContentHash),
                CurrentDependencyFingerprint = PolicyProtoMapper.ToHashBytes(request.CurrentDependencyFingerprint),
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.PolicyBinding> ExpireExceptionBinding(
        ExpireExceptionBindingRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<PolicyBindingView> result = await _expireException.ExecuteAsync(
            new ExpireExceptionBindingCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                BindingId = ProtoUuid.ToGuid(request.BindingId),
                ExpectedRowVersion = request.ExpectedRowVersion,
            },
            context.CancellationToken).ConfigureAwait(false);
        return PolicyProtoMapper.ToProto(Unwrap(result));
    }

    private string ResolveActor(ServerCallContext context)
    {
        string? actor = context.RequestHeaders.GetValue(ActorMetadataKey);
        if (!string.IsNullOrWhiteSpace(actor))
        {
            return actor.Trim();
        }

        if (_environment.IsDevelopment())
        {
            return "dev-actor";
        }

        throw GrpcApplicationErrorMapper.ToRpcException(
            ApplicationError.Unauthorized("x-mfc-actor metadata is required."));
    }

    private static T Unwrap<T>(ApplicationResult<T> result)
    {
        if (result.IsFailure)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(result.Error!);
        }

        return result.Value!;
    }
}
