using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Desired policy binding (Policy Model §10). Activation never starts deployment.
/// Exception expiry transitions to EXPIRED_PENDING_RECONCILIATION only.
/// </summary>
public sealed class PolicyDesiredBinding
{
    public const int MaxActiveExceptionsPerScope = 256;

    public PolicyBindingId Id { get; }

    public PolicyBindingScope Scope { get; }

    public Guid? ScopeId { get; }

    public PolicyId PolicyId { get; }

    public PolicyRevisionId DesiredRevisionId { get; }

    public PolicyAnalysisRunId AnalysisRunId { get; }

    public Hash256 BundleHash { get; }

    public PolicyBindingState State { get; private set; }

    public DateTimeOffset? ValidFromUtc { get; }

    public DateTimeOffset? ValidUntilUtc { get; }

    public ulong RowVersion { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private PolicyDesiredBinding(
        PolicyBindingId id,
        PolicyBindingScope scope,
        Guid? scopeId,
        PolicyId policyId,
        PolicyRevisionId desiredRevisionId,
        PolicyAnalysisRunId analysisRunId,
        Hash256 bundleHash,
        PolicyBindingState state,
        DateTimeOffset? validFromUtc,
        DateTimeOffset? validUntilUtc,
        ulong rowVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Scope = scope;
        ScopeId = scopeId;
        PolicyId = policyId;
        DesiredRevisionId = desiredRevisionId;
        AnalysisRunId = analysisRunId;
        BundleHash = bundleHash;
        State = state;
        ValidFromUtc = validFromUtc;
        ValidUntilUtc = validUntilUtc;
        RowVersion = rowVersion;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>Creates an ACTIVE desired binding. Does not compile or deploy.</summary>
    public static PolicyDesiredBinding Activate(
        Policy policy,
        PolicyRevision revision,
        PolicyAnalysisRun run,
        DateTimeOffset nowUtc,
        DateTimeOffset? validFromUtc,
        DateTimeOffset? validUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(run);
        if (revision.PolicyId != policy.Id)
        {
            throw new DomainInvariantException("Binding revision must belong to the policy container.");
        }

        PolicyBindingScope scope = ScopeFor(policy.Kind);
        Guid? scopeId = scope == PolicyBindingScope.Company ? null : policy.OwnerId;
        if (scope != PolicyBindingScope.Company && (scopeId is null || scopeId == Guid.Empty))
        {
            throw new DomainInvariantException($"{scope} binding requires a concrete owner_id.");
        }

        if (scope == PolicyBindingScope.Exception)
        {
            if (validUntilUtc is null)
            {
                throw new DomainInvariantException("EXCEPTION binding requires valid_until.");
            }
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        return new PolicyDesiredBinding(
            PolicyBindingId.New(),
            scope,
            scopeId,
            policy.Id,
            revision.Id,
            run.Id,
            run.BundleHash,
            PolicyBindingState.Active,
            validFromUtc?.ToUniversalTime(),
            validUntilUtc?.ToUniversalTime(),
            rowVersion: 1,
            now,
            now);
    }

    /// <summary>Rebuilds a binding from persistence.</summary>
    public static PolicyDesiredBinding Reconstitute(
        PolicyBindingId id,
        PolicyBindingScope scope,
        Guid? scopeId,
        PolicyId policyId,
        PolicyRevisionId desiredRevisionId,
        PolicyAnalysisRunId analysisRunId,
        Hash256 bundleHash,
        PolicyBindingState state,
        DateTimeOffset? validFromUtc,
        DateTimeOffset? validUntilUtc,
        ulong rowVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bundleHash);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("Binding row_version must be greater than zero.");
        }

        return new PolicyDesiredBinding(
            id,
            scope,
            scopeId,
            policyId,
            desiredRevisionId,
            analysisRunId,
            bundleHash,
            state,
            validFromUtc,
            validUntilUtc,
            rowVersion,
            createdAtUtc.ToUniversalTime(),
            updatedAtUtc.ToUniversalTime());
    }

    /// <summary>Disables an ACTIVE binding. Does not deploy.</summary>
    public void Disable(DateTimeOffset nowUtc)
    {
        if (State != PolicyBindingState.Active)
        {
            throw new DomainInvariantException("Only ACTIVE bindings may be disabled.");
        }

        State = PolicyBindingState.Disabled;
        RowVersion++;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    /// <summary>
    /// Marks an expired EXCEPTION binding. Deployed firewall is unchanged.
    /// </summary>
    public void ExpirePendingReconciliation(DateTimeOffset nowUtc)
    {
        if (Scope != PolicyBindingScope.Exception)
        {
            throw new DomainInvariantException("Only EXCEPTION bindings may expire.");
        }

        if (State != PolicyBindingState.Active)
        {
            throw new DomainInvariantException("Only ACTIVE exception bindings may expire.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        if (ValidUntilUtc is null || now < ValidUntilUtc.Value)
        {
            throw new DomainInvariantException("Exception binding is not past valid_until.");
        }

        State = PolicyBindingState.ExpiredPendingReconciliation;
        RowVersion++;
        UpdatedAtUtc = now;
    }

    /// <summary>Maps policy kind onto binding scope (Policy Model §10).</summary>
    public static PolicyBindingScope ScopeFor(PolicyKind kind)
        => kind switch
        {
            PolicyKind.CompanyBaseline => PolicyBindingScope.Company,
            PolicyKind.SiteOverlay => PolicyBindingScope.Site,
            PolicyKind.NodeOverlay => PolicyBindingScope.Node,
            PolicyKind.Exception => PolicyBindingScope.Exception,
            PolicyKind.IncidentDenyOverlay => PolicyBindingScope.IncidentDenyOverlay,
            _ => throw new DomainInvariantException($"Unknown policy kind '{kind}'."),
        };
}
