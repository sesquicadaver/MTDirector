using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
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
/// then runs <see cref="EffectivePolicyComposer"/>. Layer revisions come from ACTIVE
/// desired bindings (AUDIT-BIND-01 / F07), not latest Approved. Compose blockers keep typed
/// <c>POLICY_COMPOSE_*</c> / <c>RULE_*</c> / <c>PREDICATE_*</c> / <c>POLICY_EXCEPTION_*</c>
/// plus sequence <c>SHADOW_ANALYSIS_INDETERMINATE</c> / <c>EARLIER_ALLOW_BYPASSES_DENY</c> /
/// <c>FASTTRACK_OVERLAP</c> codes.
/// </summary>
public sealed class ComposeEffectivePolicyUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IZoneDefinitionStore _zones;
    private readonly IClock _clock;

    public ComposeEffectivePolicyUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IZoneDefinitionStore zones,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _nodes = nodes;
        _policies = policies;
        _approvals = approvals;
        _zones = zones;
        _clock = clock;
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
            await PolicyBoundLayerLoader.LoadBoundLayerAsync(
                    _policies,
                    _approvals,
                    companies[0],
                    required: true,
                    cancellationToken)
                .ConfigureAwait(false);
        if (companyError is not null)
        {
            return ApplicationResults.Fail(companyError);
        }

        if (companyLayer is null || companyRef is null)
        {
            return ComposeFail(
                PolicyComposeCodes.CompanyRequired,
                "Company baseline has no ACTIVE desired binding.");
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

        (IReadOnlyList<PolicyLayer>? exceptionLayers, ApplicationError? exceptionError) =
            await LoadExceptionsAsync(node, cancellationToken).ConfigureAwait(false);
        if (exceptionError is not null)
        {
            return ApplicationResults.Fail(exceptionError);
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
            knownZoneIds,
            exceptionLayers);
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

        return await PolicyBoundLayerLoader.LoadBoundLayerAsync(
                _policies,
                _approvals,
                overlays[0],
                required: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(IReadOnlyList<PolicyLayer>? Layers, ApplicationError? Error)> LoadExceptionsAsync(
        Node node,
        CancellationToken cancellationToken)
    {
        List<PolicyLayer> layers = [];
        ApplicationError? siteError = await PolicyBoundLayerLoader.AppendBoundExceptionLayersAsync(
            _policies,
            _approvals,
            node.SiteId.Value,
            _clock.UtcNow,
            layers,
            cancellationToken).ConfigureAwait(false);
        if (siteError is not null)
        {
            return (null, siteError);
        }

        ApplicationError? nodeError = await PolicyBoundLayerLoader.AppendBoundExceptionLayersAsync(
            _policies,
            _approvals,
            node.Id.Value,
            _clock.UtcNow,
            layers,
            cancellationToken).ConfigureAwait(false);
        if (nodeError is not null)
        {
            return (null, nodeError);
        }

        return (layers, null);
    }

    private static ApplicationFailure ComposeFail(string code, string message)
        => ApplicationResults.Fail(new ApplicationError(code, message));
}
