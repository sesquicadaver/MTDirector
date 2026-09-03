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

/// <summary>Input for typed EXCEPTION metadata (M2-08).</summary>
public sealed class ExceptionMetadataInput
{
    public required PolicyOwnerScope TargetScope { get; init; }

    public required Guid TargetScopeId { get; init; }

    public required PolicyPipelineStage TargetStage { get; init; }

    public required Guid WaivedRuleId { get; init; }

    public required DateTimeOffset ValidFrom { get; init; }

    public required DateTimeOffset ValidUntil { get; init; }

    public required string Reason { get; init; }

    public required string TicketReference { get; init; }

    public Guid? SupersedesExceptionId { get; init; }
}

/// <summary>CAS draft mutation for EXCEPTION metadata.</summary>
public sealed class UpdateExceptionMetadataCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required ExceptionMetadataInput Metadata { get; init; }
}

/// <summary>Sets typed exception_metadata on a DRAFT EXCEPTION revision (LOCK-12 / LOCK-4′).</summary>
public sealed class UpdateExceptionMetadataUseCase
{
    public const string Operation = "policy.update_exception_metadata";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly INodeStore _nodes;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateExceptionMetadataUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        INodeStore nodes,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _policies = policies;
        _nodes = nodes;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        UpdateExceptionMetadataCommand command,
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
            target_scope = command.Metadata.TargetScope.ToString(),
            command.Metadata.TargetScopeId,
            target_stage = command.Metadata.TargetStage.ToString(),
            command.Metadata.WaivedRuleId,
            valid_from = ExceptionMetadata.FormatTimestamp(command.Metadata.ValidFrom),
            valid_until = ExceptionMetadata.FormatTimestamp(command.Metadata.ValidUntil),
            command.Metadata.Reason,
            command.Metadata.TicketReference,
            command.Metadata.SupersedesExceptionId,
        });
        ApplicationResult<PolicyRevisionView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (revisionId, ct) => await LoadViewAsync(revisionId, ct).ConfigureAwait(false),
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

        ApplicationError? editable = PolicyRevisionSupport.EnsureEditable(revision!);
        if (editable is not null)
        {
            return ApplicationResults.Fail(editable);
        }

        Policy? policy = await _policies.GetPolicyAsync(revision!.PolicyId, cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy '{revision.PolicyId}' was not found."));
        }

        if (policy.Kind != PolicyKind.Exception)
        {
            return ApplicationResults.Fail(
                ApplicationError.Validation("UpdateExceptionMetadata applies only to EXCEPTION revisions."));
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision);
        if (document.IsFailure)
        {
            return ApplicationResults.Fail(document.Error!);
        }

        ExceptionMetadata metadata;
        try
        {
            metadata = ExceptionMetadata.Create(
                command.Metadata.TargetScope,
                command.Metadata.TargetScopeId,
                command.Metadata.TargetStage,
                new RuleId(command.Metadata.WaivedRuleId),
                command.Metadata.ValidFrom,
                command.Metadata.ValidUntil,
                command.Metadata.Reason,
                command.Metadata.TicketReference,
                command.Metadata.SupersedesExceptionId);
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        if (metadata.TargetScope != policy.OwnerScope || metadata.TargetScopeId != policy.OwnerId)
        {
            return ApplicationResults.Fail(new ApplicationError(
                PolicyExceptionCodes.MetadataInvalid,
                "exception_metadata target_scope/target_scope_id must match the EXCEPTION policy owner."));
        }

        PolicyDocument next = document.Value!.WithExceptionMetadata(metadata);
        Hash256? parent = revision.ParentContextHash;
        Hash256? recomputed = await TryComputeParentContextAsync(
            policy, metadata, cancellationToken).ConfigureAwait(false);
        if (recomputed is not null)
        {
            parent = recomputed;
        }

        try
        {
            revision.ReplaceDocument(next, parent);
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
                        JsonSerializer.Serialize(new
                        {
                            revision_id = revision.Id.Value,
                            content_hash = revision.ContentHash.ToString(),
                        }),
                        ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(ViewMapper.ToView(revision, next));
    }

    private async Task<ApplicationResult<PolicyRevisionView>> LoadViewAsync(
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

    private async Task<Hash256?> TryComputeParentContextAsync(
        Policy policy,
        ExceptionMetadata metadata,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> companies = await _policies
            .ListActiveByKindAsync(PolicyKind.CompanyBaseline, cancellationToken)
            .ConfigureAwait(false);
        if (companies.Count != 1)
        {
            return null;
        }

        PolicyRevision? companyRev = await LatestApprovedAsync(companies[0].Id, cancellationToken)
            .ConfigureAwait(false);
        if (companyRev is null)
        {
            return null;
        }

        ApplicationResult<PolicyDocument> companyDoc = PolicyRevisionSupport.ReadDocument(companyRev);
        if (companyDoc.IsFailure)
        {
            return null;
        }

        List<PolicyDocument> search = [companyDoc.Value!];
        Hash256? siteHash = null;
        Hash256? nodeHash = null;

        if (policy.OwnerScope == PolicyOwnerScope.Site && policy.OwnerId is Guid siteId)
        {
            (PolicyDocument? siteDoc, Hash256? hash) = await LoadUniqueOverlayAsync(
                PolicyKind.SiteOverlay, siteId, cancellationToken).ConfigureAwait(false);
            if (siteDoc is not null)
            {
                search.Add(siteDoc);
                siteHash = hash;
            }
        }
        else if (policy.OwnerScope == PolicyOwnerScope.Node && policy.OwnerId is Guid nodeId)
        {
            Domain.Inventory.Node? node = await _nodes
                .GetAsync(new Domain.Inventory.Primitives.NodeId(nodeId), cancellationToken)
                .ConfigureAwait(false);
            if (node is not null)
            {
                (PolicyDocument? siteDoc, Hash256? loadedSiteHash) = await LoadUniqueOverlayAsync(
                    PolicyKind.SiteOverlay, node.SiteId.Value, cancellationToken).ConfigureAwait(false);
                if (siteDoc is not null)
                {
                    search.Add(siteDoc);
                    siteHash = loadedSiteHash;
                }

                (PolicyDocument? nodeDoc, Hash256? loadedNodeHash) = await LoadUniqueOverlayAsync(
                    PolicyKind.NodeOverlay, node.Id.Value, cancellationToken).ConfigureAwait(false);
                if (nodeDoc is not null)
                {
                    search.Add(nodeDoc);
                    nodeHash = loadedNodeHash;
                }
            }
        }

        PolicyRule? target = search.SelectMany(static d => d.Rules)
            .FirstOrDefault(r => r.Id == metadata.WaivedRuleId);
        if (target is null)
        {
            return null;
        }

        Hash256 waived = PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(target));
        Hash256? nodeSlot = policy.OwnerScope == PolicyOwnerScope.Node ? nodeHash : null;
        return PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception,
            companyRev.ContentHash,
            siteHash,
            nodeSlot,
            waived);
    }

    private async Task<(PolicyDocument? Document, Hash256? ContentHash)> LoadUniqueOverlayAsync(
        PolicyKind kind,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> overlays = await _policies
            .ListActiveByOwnerAsync(kind, ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (overlays.Count != 1)
        {
            return (null, null);
        }

        PolicyRevision? approved = await LatestApprovedAsync(overlays[0].Id, cancellationToken)
            .ConfigureAwait(false);
        if (approved is null)
        {
            return (null, null);
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(approved);
        return document.IsFailure ? (null, null) : (document.Value, approved.ContentHash);
    }

    private async Task<PolicyRevision?> LatestApprovedAsync(PolicyId policyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyRevision> revisions = await _policies
            .ListRevisionsAsync(policyId, cancellationToken)
            .ConfigureAwait(false);
        return revisions
            .Where(static r => r.State == PolicyRevisionState.Approved)
            .OrderByDescending(static r => r.RevisionNumber)
            .FirstOrDefault();
    }
}
