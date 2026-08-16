using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

public sealed class DiffPolicyRevisionsQuery
{
    public required string Actor { get; init; }

    public required Guid BeforeRevisionId { get; init; }

    public required Guid AfterRevisionId { get; init; }
}

/// <summary>
/// Loads two revisions and returns UUID-keyed semantic diff + risk (M2-18).
/// Uses <see cref="PolicyEvidenceSignals.None"/>; no RouterOS.
/// </summary>
public sealed class DiffPolicyRevisionsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;

    public DiffPolicyRevisionsUseCase(IAuthorizationBoundary auth, IPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        _auth = auth;
        _policies = policies;
    }

    public async Task<ApplicationResult<PolicyRevisionDiffView>> ExecuteAsync(
        DiffPolicyRevisionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.PolicyRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationResult<(PolicyRevision Revision, PolicyDocument Document, Policy Policy)> before =
            await LoadAsync(query.BeforeRevisionId, cancellationToken).ConfigureAwait(false);
        if (before.IsFailure)
        {
            return ApplicationResults.Fail(before.Error!);
        }

        ApplicationResult<(PolicyRevision Revision, PolicyDocument Document, Policy Policy)> after =
            await LoadAsync(query.AfterRevisionId, cancellationToken).ConfigureAwait(false);
        if (after.IsFailure)
        {
            return ApplicationResults.Fail(after.Error!);
        }

        PolicyObjectIdentity beforeOwner = PolicyCatalogViewMapper.DeriveObjectIdentity(
            before.Value!.Policy, before.Value.Revision);
        PolicyObjectIdentity afterOwner = PolicyCatalogViewMapper.DeriveObjectIdentity(
            after.Value!.Policy, after.Value.Revision);

        if (!PolicyCatalogViewMapper.TryParseTypedAddresses(
                before.Value.Document, beforeOwner, out Dictionary<AddressObjectId, AddressObject> beforeAddr, out string? error)
            || !PolicyCatalogViewMapper.TryParseTypedServices(
                before.Value.Document, beforeOwner, out Dictionary<ServiceObjectId, ServiceObject> beforeSvc, out error)
            || !PolicyCatalogViewMapper.TryParseTypedAddresses(
                after.Value.Document, afterOwner, out Dictionary<AddressObjectId, AddressObject> afterAddr, out error)
            || !PolicyCatalogViewMapper.TryParseTypedServices(
                after.Value.Document, afterOwner, out Dictionary<ServiceObjectId, ServiceObject> afterSvc, out error))
        {
            return ApplicationResults.Fail(ApplicationError.Validation(error ?? "Catalog parse failed."));
        }

        HashSet<Guid> beforeZones = PolicyCatalogViewMapper.ExtractZoneIds(before.Value.Document);
        HashSet<Guid> afterZones = PolicyCatalogViewMapper.ExtractZoneIds(after.Value.Document);
        PolicyRevisionDiffResult diff = PolicyRevisionDiffer.Diff(
            before.Value.Document.Rules,
            after.Value.Document.Rules,
            beforeAddr,
            afterAddr,
            beforeSvc,
            afterSvc,
            beforeZones,
            afterZones);
        PolicyRiskResult risk = PolicyRiskClassifier.Classify(
            diff,
            [],
            PolicyEvidenceSignals.None,
            before.Value.Document.Rules,
            after.Value.Document.Rules);

        return ApplicationResults.Ok(new PolicyRevisionDiffView
        {
            BeforeRevisionId = before.Value.Revision.Id.Value,
            AfterRevisionId = after.Value.Revision.Id.Value,
            RuleChanges = diff.RuleChanges.Select(static e => new PolicyRuleDiffLineView
            {
                RuleId = e.RuleId.Value,
                Changes = e.Changes,
            }).ToArray(),
            SemanticClasses = diff.SemanticClasses,
            PacketSpaceClasses = diff.PacketSpaceClasses,
            RiskLevel = risk.Level,
            RiskDrivers = risk.Drivers,
            FindingSummaries = [],
        });
    }

    private async Task<ApplicationResult<(PolicyRevision Revision, PolicyDocument Document, Policy Policy)>> LoadAsync(
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

        Policy? policy = await _policies.GetPolicyAsync(revision!.PolicyId, cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Policy '{revision.PolicyId}' was not found."));
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision);
        if (document.IsFailure)
        {
            return ApplicationResults.Fail(document.Error!);
        }

        return ApplicationResults.Ok((revision, document.Value!, policy));
    }
}
