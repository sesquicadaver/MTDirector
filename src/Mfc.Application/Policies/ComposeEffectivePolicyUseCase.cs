using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

/// <summary>Query for compute-on-read logical effective policy (M2-07).</summary>
public sealed class ComposeEffectivePolicyQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }
}

/// <summary>
/// Loads inventory + unique ACTIVE company + optional overlays + zone catalog,
/// then runs <see cref="EffectivePolicyComposer"/>. Compose blockers keep <c>POLICY_COMPOSE_*</c> codes.
/// </summary>
public sealed class ComposeEffectivePolicyUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IPolicyStore _policies;
    private readonly IZoneDefinitionStore _zones;

    public ComposeEffectivePolicyUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IPolicyStore policies,
        IZoneDefinitionStore zones)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(zones);
        _auth = auth;
        _nodes = nodes;
        _policies = policies;
        _zones = zones;
    }

    public async Task<ApplicationResult<EffectivePolicyView>> ExecuteAsync(
        ComposeEffectivePolicyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Node? node = await _nodes.GetAsync(new NodeId(query.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{query.NodeId}' was not found."));
        }

        IReadOnlyList<Policy> companies = await _policies
            .ListActiveByKindAsync(PolicyKind.CompanyBaseline, cancellationToken)
            .ConfigureAwait(false);
        if (companies.Count == 0)
        {
            return ComposeFail(
                PolicyComposeCodes.CompanyRequired,
                "Exactly one ACTIVE company baseline is required for composition.");
        }

        if (companies.Count != 1)
        {
            return ComposeFail(
                PolicyComposeCodes.PolicyNotUnique,
                "Exactly one ACTIVE company baseline is required; duplicates are forbidden.");
        }

        (PolicyLayer? companyLayer, PolicyRevisionRefView? companyRef, ApplicationError? companyError) =
            await LoadApprovedLayerAsync(companies[0], cancellationToken).ConfigureAwait(false);
        if (companyError is not null)
        {
            return ApplicationResults.Fail(companyError);
        }

        if (companyLayer is null || companyRef is null)
        {
            return ComposeFail(
                PolicyComposeCodes.CompanyRequired,
                "Company baseline has no APPROVED revision.");
        }

        (PolicyLayer? siteLayer, PolicyRevisionRefView? siteRef, ApplicationError? siteError) =
            await LoadOptionalOverlayAsync(
                PolicyKind.SiteOverlay,
                node.SiteId.Value,
                cancellationToken).ConfigureAwait(false);
        if (siteError is not null)
        {
            return ApplicationResults.Fail(siteError);
        }

        (PolicyLayer? nodeLayer, PolicyRevisionRefView? nodeRef, ApplicationError? nodeError) =
            await LoadOptionalOverlayAsync(
                PolicyKind.NodeOverlay,
                node.Id.Value,
                cancellationToken).ConfigureAwait(false);
        if (nodeError is not null)
        {
            return ApplicationResults.Fail(nodeError);
        }

        IReadOnlyList<ZoneDefinition> zones = await _zones.ListAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> knownZoneIds = zones.Select(static z => z.Id.Value).ToHashSet();

        PolicyComposeResult composed = EffectivePolicyComposer.Compose(
            companyLayer,
            siteLayer,
            nodeLayer,
            node.Id.Value,
            node.SiteId.Value,
            knownZoneIds);
        if (composed.IsFailure)
        {
            return ComposeFail(composed.Code!, composed.Message!);
        }

        ComposedEffectivePolicy value = composed.Value!;
        PolicyRuleView[] rules = value.ActiveRules.Select(static r => ViewMapper.ToView(r)).ToArray();
        PolicyWarningView[] findings = value.Findings.Select(static f => new PolicyWarningView
        {
            Code = f.Code,
            Message = f.Message,
            Subject = f.Subject,
        }).ToArray();

        return ApplicationResults.Ok(new EffectivePolicyView
        {
            NodeId = node.Id.Value,
            LogicalEffectiveHash = value.LogicalEffectiveHash.Bytes.ToArray(),
            LogicalEffectiveHashHex = value.LogicalEffectiveHash.ToString(),
            Company = companyRef,
            Site = siteRef,
            Node = nodeRef,
            ActiveRules = rules,
            Findings = findings,
        });
    }

    private async Task<(PolicyLayer? Layer, PolicyRevisionRefView? Ref, ApplicationError? Error)>
        LoadOptionalOverlayAsync(PolicyKind kind, Guid ownerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> overlays = await _policies
            .ListActiveByOwnerAsync(kind, ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (overlays.Count == 0)
        {
            return (null, null, null);
        }

        if (overlays.Count != 1)
        {
            return (null, null, new ApplicationError(
                PolicyComposeCodes.PolicyNotUnique,
                $"Exactly one ACTIVE {kind} policy is allowed per owner; duplicates are forbidden."));
        }

        return await LoadApprovedLayerAsync(overlays[0], cancellationToken).ConfigureAwait(false);
    }

    private async Task<(PolicyLayer? Layer, PolicyRevisionRefView? Ref, ApplicationError? Error)>
        LoadApprovedLayerAsync(Policy policy, CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyRevision> revisions = await _policies
            .ListRevisionsAsync(policy.Id, cancellationToken)
            .ConfigureAwait(false);
        PolicyRevision? approved = revisions
            .Where(static r => r.State == PolicyRevisionState.Approved)
            .OrderByDescending(static r => r.RevisionNumber)
            .FirstOrDefault();
        if (approved is null)
        {
            return (null, null, null);
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(approved);
        if (document.IsFailure)
        {
            return (null, null, document.Error);
        }

        PolicyLayer layer = new()
        {
            Kind = policy.Kind,
            OwnerScope = policy.OwnerScope,
            OwnerId = policy.OwnerId,
            ContentHash = approved.ContentHash,
            ParentContextHash = approved.ParentContextHash,
            PolicyDocument = document.Value!,
        };
        PolicyRevisionRefView refs = new()
        {
            PolicyId = policy.Id.Value,
            RevisionId = approved.Id.Value,
            RevisionNumber = approved.RevisionNumber,
            ContentHash = approved.ContentHash.Bytes.ToArray(),
            ContentHashHex = approved.ContentHash.ToString(),
        };
        return (layer, refs, null);
    }

    private static ApplicationFailure ComposeFail(string code, string message)
        => ApplicationResults.Fail(new ApplicationError(code, message));
}
