using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// Loads compose/compile layers from ACTIVE desired bindings (AUDIT-BIND-01 / F07).
/// Approval alone never selects a revision; activation does.
/// </summary>
internal static class PolicyBoundLayerLoader
{
    /// <summary>
    /// Resolves the layer for <paramref name="policy"/> from its ACTIVE desired binding's
    /// <see cref="PolicyDesiredBinding.DesiredRevisionId"/>. When <paramref name="required"/>
    /// and no ACTIVE binding exists, returns <see cref="PolicyComposeCodes.CompanyRequired"/>.
    /// </summary>
    public static async Task<(PolicyLayer? Layer, PolicyRevisionRefView? Ref, ApplicationError? Error)>
        LoadBoundLayerAsync(
            IPolicyStore policies,
            IPolicyApprovalStore approvals,
            Policy policy,
            bool required,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(policy);

        PolicyBindingScope scope = PolicyDesiredBinding.ScopeFor(policy.Kind);
        Guid? scopeId = scope == PolicyBindingScope.Company ? null : policy.OwnerId;
        IReadOnlyList<PolicyDesiredBinding> bindings = await approvals
            .ListActiveBindingsAsync(scope, scopeId, cancellationToken)
            .ConfigureAwait(false);
        PolicyDesiredBinding? binding = bindings.FirstOrDefault(b =>
            b.PolicyId == policy.Id && b.State == PolicyBindingState.Active);
        if (binding is null)
        {
            if (required)
            {
                return (null, null, new ApplicationError(
                    PolicyComposeCodes.CompanyRequired,
                    "Company baseline has no ACTIVE desired binding."));
            }

            return (null, null, null);
        }

        PolicyRevision? revision = await policies
            .GetRevisionAsync(binding.DesiredRevisionId, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null || revision.PolicyId != policy.Id)
        {
            return (null, null, new ApplicationError(
                "validation",
                $"Desired revision '{binding.DesiredRevisionId.Value}' for policy '{policy.Id.Value}' was not found."));
        }

        if (revision.State != PolicyRevisionState.Approved)
        {
            if (required)
            {
                return (null, null, new ApplicationError(
                    PolicyComposeCodes.CompanyRequired,
                    "Company baseline desired revision is not APPROVED."));
            }

            return (null, null, null);
        }

        return ToLayer(policy, revision);
    }

    /// <summary>
    /// Loads ACTIVE exception bindings for <paramref name="ownerId"/> and materializes layers
    /// from each binding's desired revision (skips expired exception metadata).
    /// </summary>
    public static async Task<ApplicationError?> AppendBoundExceptionLayersAsync(
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        Guid ownerId,
        DateTimeOffset nowUtc,
        List<PolicyLayer> layers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(layers);

        IReadOnlyList<PolicyDesiredBinding> bindings = await approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Exception, ownerId, cancellationToken)
            .ConfigureAwait(false);
        foreach (PolicyDesiredBinding binding in bindings
                     .Where(static b => b.State == PolicyBindingState.Active)
                     .OrderBy(static b => b.Id.Value))
        {
            Policy? policy = await policies.GetPolicyAsync(binding.PolicyId, cancellationToken)
                .ConfigureAwait(false);
            if (policy is null
                || policy.Kind != PolicyKind.Exception
                || policy.Status != PolicyStatus.Active
                || policy.OwnerId != ownerId)
            {
                continue;
            }

            (PolicyLayer? layer, _, ApplicationError? error) = await LoadBoundLayerAsync(
                    policies,
                    approvals,
                    policy,
                    required: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
            {
                return error;
            }

            if (layer is null)
            {
                continue;
            }

            ExceptionMetadata? metadata = layer.PolicyDocument.ExceptionMetadata;
            if (metadata is not null && metadata.IsExpired(nowUtc))
            {
                continue;
            }

            layers.Add(layer);
        }

        return null;
    }

    /// <summary>
    /// Loads ACTIVE incident-deny-overlay bindings for <paramref name="nodeId"/> from desired revisions.
    /// </summary>
    public static async Task<(IReadOnlyList<PolicyLayer>? Layers, ApplicationError? Error)>
        LoadBoundIncidentOverlayLayersAsync(
            IPolicyStore policies,
            IPolicyApprovalStore approvals,
            Guid nodeId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);

        List<PolicyLayer> layers = [];
        IReadOnlyList<PolicyDesiredBinding> bindings = await approvals
            .ListActiveBindingsAsync(PolicyBindingScope.IncidentDenyOverlay, nodeId, cancellationToken)
            .ConfigureAwait(false);
        foreach (PolicyDesiredBinding binding in bindings
                     .Where(static b => b.State == PolicyBindingState.Active)
                     .OrderBy(static b => b.Id.Value))
        {
            Policy? policy = await policies.GetPolicyAsync(binding.PolicyId, cancellationToken)
                .ConfigureAwait(false);
            if (policy is null
                || policy.Kind != PolicyKind.IncidentDenyOverlay
                || policy.Status != PolicyStatus.Active
                || policy.OwnerId != nodeId)
            {
                continue;
            }

            (PolicyLayer? layer, _, ApplicationError? error) = await LoadBoundLayerAsync(
                    policies,
                    approvals,
                    policy,
                    required: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
            {
                return (null, error);
            }

            if (layer is null)
            {
                continue;
            }

            IncidentDenyOverlayMetadata? metadata = layer.PolicyDocument.IncidentDenyOverlayMetadata;
            if (metadata is not null && metadata.IsExpired(nowUtc))
            {
                continue;
            }

            if (metadata is not null && metadata.NodeId != nodeId)
            {
                return (null, new ApplicationError(
                    IncidentDenyOverlayCodes.OverlayNodeMismatch,
                    "Incident deny overlay node_id must match the compile target Node."));
            }

            layers.Add(layer);
        }

        return (layers, null);
    }

    private static (PolicyLayer? Layer, PolicyRevisionRefView? Ref, ApplicationError? Error) ToLayer(
        Policy policy,
        PolicyRevision revision)
    {
        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision);
        if (document.IsFailure)
        {
            return (null, null, document.Error);
        }

        PolicyLayer layer = new()
        {
            PolicyId = policy.Id.Value,
            RevisionId = revision.Id.Value,
            Kind = policy.Kind,
            OwnerScope = policy.OwnerScope,
            OwnerId = policy.OwnerId,
            ContentHash = revision.ContentHash,
            ParentContextHash = revision.ParentContextHash,
            PolicyDocument = document.Value!,
        };
        PolicyRevisionRefView refs = new()
        {
            PolicyId = policy.Id.Value,
            RevisionId = revision.Id.Value,
            RevisionNumber = revision.RevisionNumber,
            ContentHash = revision.ContentHash.Bytes.ToArray(),
            ContentHashHex = revision.ContentHash.ToString(),
        };
        return (layer, refs, null);
    }
}
