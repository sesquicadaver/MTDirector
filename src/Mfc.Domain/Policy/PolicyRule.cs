using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Typed managed filter rule (Policy Model §23).</summary>
public sealed class PolicyRule
{
    public RuleId Id { get; }

    public IpAddressFamily Family { get; }

    public PolicyFilterChain Chain { get; }

    public PolicyPipelineStage Stage { get; }

    public uint Ordinal { get; }

    public bool Enabled { get; }

    public TrafficPredicate Predicate { get; }

    public RuleEffectSpec Effect { get; }

    public LogSpecification Logging { get; }

    public bool ExceptionEligible { get; }

    public string Description { get; }

    private PolicyRule(
        RuleId id,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        uint ordinal,
        bool enabled,
        TrafficPredicate predicate,
        RuleEffectSpec effect,
        LogSpecification logging,
        bool exceptionEligible,
        string description)
    {
        Id = id;
        Family = family;
        Chain = chain;
        Stage = stage;
        Ordinal = ordinal;
        Enabled = enabled;
        Predicate = predicate;
        Effect = effect;
        Logging = logging;
        ExceptionEligible = exceptionEligible;
        Description = description;
    }

    /// <summary>Creates and structurally validates a rule (Policy Model §23 / §26).</summary>
    public static PolicyRule Create(
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        uint ordinal,
        TrafficPredicate predicate,
        RuleEffectSpec effect,
        LogSpecification? logging = null,
        bool enabled = true,
        bool exceptionEligible = false,
        string? description = null,
        RuleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(effect);
        ValidateCore(
            family,
            chain,
            stage,
            predicate,
            effect,
            exceptionEligible,
            requireTcpOnlyForReset: true);
        return new PolicyRule(
            id ?? RuleId.New(),
            family,
            chain,
            stage,
            ordinal,
            enabled,
            predicate,
            effect,
            logging ?? LogSpecification.Disabled,
            exceptionEligible,
            NormalizeDescription(description));
    }

    /// <summary>
    /// Rebuilds a rule from canonical bytes. Skips catalog-dependent TCP_RESET proof
    /// (already persisted); still enforces structural invariants.
    /// </summary>
    public static PolicyRule Reconstitute(
        RuleId id,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        uint ordinal,
        bool enabled,
        TrafficPredicate predicate,
        RuleEffectSpec effect,
        LogSpecification logging,
        bool exceptionEligible,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(logging);
        ValidateCore(
            family,
            chain,
            stage,
            predicate,
            effect,
            exceptionEligible,
            requireTcpOnlyForReset: false);
        return new PolicyRule(
            id,
            family,
            chain,
            stage,
            ordinal,
            enabled,
            predicate,
            effect,
            logging,
            exceptionEligible,
            NormalizeDescription(description));
    }

    /// <summary>Returns a copy with a replacement ordinal (list helpers use this).</summary>
    public PolicyRule WithOrdinal(uint ordinal)
        => new(
            Id,
            Family,
            Chain,
            Stage,
            ordinal,
            Enabled,
            Predicate,
            Effect,
            Logging,
            ExceptionEligible,
            Description);

    private static void ValidateCore(
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        TrafficPredicate predicate,
        RuleEffectSpec effect,
        bool exceptionEligible,
        bool requireTcpOnlyForReset)
    {
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported rule family '{family}'.");
        }

        if (chain is not (PolicyFilterChain.Input or PolicyFilterChain.Forward or PolicyFilterChain.Output))
        {
            throw new DomainInvariantException($"Unsupported rule chain '{chain}'.");
        }

        if (stage == PolicyPipelineStage.DefaultDisposition)
        {
            throw new DomainInvariantException(
                "DEFAULT_DISPOSITION cannot host editable policy rules.");
        }

        PolicyPipelineV1.EnsureAllowedEffect(stage, effect.Kind);
        ZoneSelector.EnsureAllowedOnChain(chain, predicate.IngressZones, predicate.EgressZones);

        if (exceptionEligible)
        {
            if (effect.Kind is not (PolicyRuleEffect.Drop or PolicyRuleEffect.Reject))
            {
                throw new DomainInvariantException(
                    "exception_eligible is allowed only for DROP or REJECT effects.");
            }

            if (stage == PolicyPipelineStage.MandatoryPreStateDeny)
            {
                throw new DomainInvariantException(
                    "exception_eligible is forbidden on MANDATORY_PRE_STATE_DENY.");
            }
        }

        if (effect.Kind == PolicyRuleEffect.Reject
            && effect.RejectModeValue == RejectMode.TcpReset
            && requireTcpOnlyForReset
            && !predicate.IsTcpOnly())
        {
            throw new DomainInvariantException(
                "TCP_RESET requires a TCP-only traffic predicate.");
        }
    }

    private static string NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
}
