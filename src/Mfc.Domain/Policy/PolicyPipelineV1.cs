namespace Mfc.Domain.Policy;

/// <summary>
/// Fixed Policy Pipeline v1: stage order and owner/effect permissions (Policy Model §12–§13).
/// Stage order is never persisted as user-editable data.
/// </summary>
public static class PolicyPipelineV1
{
    public const string Version = "v1";

    public const int StageCount = 13;

    private static readonly PolicyPipelineStage[] OrderedStageValues =
    [
        PolicyPipelineStage.ProtectedControlPlane,
        PolicyPipelineStage.MandatoryPreStateDeny,
        PolicyPipelineStage.StatePrelude,
        PolicyPipelineStage.CompanyDenyExemptions,
        PolicyPipelineStage.CompanyDeny,
        PolicyPipelineStage.SiteDenyExemptions,
        PolicyPipelineStage.SiteDeny,
        PolicyPipelineStage.NodeDenyExemptions,
        PolicyPipelineStage.NodeDeny,
        PolicyPipelineStage.CompanyAllow,
        PolicyPipelineStage.SiteAllow,
        PolicyPipelineStage.NodeAllow,
        PolicyPipelineStage.DefaultDisposition,
    ];

    private static readonly (Inventory.IpAddressFamily Family, PolicyFilterChain Chain)[] OrderedSurfaceValues =
    [
        (Inventory.IpAddressFamily.IPv4, PolicyFilterChain.Input),
        (Inventory.IpAddressFamily.IPv4, PolicyFilterChain.Forward),
        (Inventory.IpAddressFamily.IPv4, PolicyFilterChain.Output),
        (Inventory.IpAddressFamily.IPv6, PolicyFilterChain.Input),
        (Inventory.IpAddressFamily.IPv6, PolicyFilterChain.Forward),
        (Inventory.IpAddressFamily.IPv6, PolicyFilterChain.Output),
    ];

    /// <summary>Normative stage order for every family/chain surface.</summary>
    public static IReadOnlyList<PolicyPipelineStage> OrderedStages { get; } = OrderedStageValues;

    /// <summary>
    /// Stages that may host policy rules. <see cref="PolicyPipelineStage.DefaultDisposition"/>
    /// is terminal and driven by <see cref="ChainContract"/>, not editable rules.
    /// </summary>
    public static IReadOnlyList<PolicyPipelineStage> RuleStages { get; }
        = OrderedStageValues.Where(static s => s != PolicyPipelineStage.DefaultDisposition).ToArray();

    /// <summary>Deterministic enumeration of IPv4/IPv6 × INPUT/FORWARD/OUTPUT surfaces.</summary>
    public static IReadOnlyList<(Inventory.IpAddressFamily Family, PolicyFilterChain Chain)> OrderedSurfaces { get; }
        = OrderedSurfaceValues;

    public static int Ordinal(PolicyPipelineStage stage)
    {
        int ordinal = (int)stage;
        if (ordinal is < 0 or >= StageCount)
        {
            throw new DomainInvariantException($"Unknown pipeline stage '{stage}'.");
        }

        return ordinal;
    }

    public static string FormatStage(PolicyPipelineStage stage)
        => stage switch
        {
            PolicyPipelineStage.ProtectedControlPlane => "PROTECTED_CONTROL_PLANE",
            PolicyPipelineStage.MandatoryPreStateDeny => "MANDATORY_PRE_STATE_DENY",
            PolicyPipelineStage.StatePrelude => "STATE_PRELUDE",
            PolicyPipelineStage.CompanyDenyExemptions => "COMPANY_DENY_EXEMPTIONS",
            PolicyPipelineStage.CompanyDeny => "COMPANY_DENY",
            PolicyPipelineStage.SiteDenyExemptions => "SITE_DENY_EXEMPTIONS",
            PolicyPipelineStage.SiteDeny => "SITE_DENY",
            PolicyPipelineStage.NodeDenyExemptions => "NODE_DENY_EXEMPTIONS",
            PolicyPipelineStage.NodeDeny => "NODE_DENY",
            PolicyPipelineStage.CompanyAllow => "COMPANY_ALLOW",
            PolicyPipelineStage.SiteAllow => "SITE_ALLOW",
            PolicyPipelineStage.NodeAllow => "NODE_ALLOW",
            PolicyPipelineStage.DefaultDisposition => "DEFAULT_DISPOSITION",
            _ => throw new DomainInvariantException($"Unknown pipeline stage '{stage}'."),
        };

    public static string FormatFilterChain(PolicyFilterChain chain)
        => chain switch
        {
            PolicyFilterChain.Input => "INPUT",
            PolicyFilterChain.Forward => "FORWARD",
            PolicyFilterChain.Output => "OUTPUT",
            _ => throw new DomainInvariantException($"Unknown filter chain '{chain}'."),
        };

    public static string FormatFamily(Inventory.IpAddressFamily family)
        => family switch
        {
            Inventory.IpAddressFamily.IPv4 => "IPv4",
            Inventory.IpAddressFamily.IPv6 => "IPv6",
            _ => throw new DomainInvariantException($"Unknown address family '{family}'."),
        };

    public static string FormatEffect(PolicyRuleEffect effect)
        => effect switch
        {
            PolicyRuleEffect.Accept => "ACCEPT",
            PolicyRuleEffect.Drop => "DROP",
            PolicyRuleEffect.Reject => "REJECT",
            PolicyRuleEffect.FasttrackAccept => "FASTTRACK_ACCEPT",
            PolicyRuleEffect.ExemptDenyStage => "EXEMPT_DENY_STAGE",
            _ => throw new DomainInvariantException($"Unknown rule effect '{effect}'."),
        };

    public static string FormatDisposition(ChainDefaultDisposition disposition)
        => disposition switch
        {
            ChainDefaultDisposition.Drop => "DROP",
            ChainDefaultDisposition.Reject => "REJECT",
            ChainDefaultDisposition.ReturnToUnmanaged => "RETURN_TO_UNMANAGED",
            _ => throw new DomainInvariantException($"Unknown default disposition '{disposition}'."),
        };

    public static string FormatRejectMode(RejectMode mode)
        => mode switch
        {
            RejectMode.TcpReset => "TCP_RESET",
            RejectMode.AdminProhibited => "ADMIN_PROHIBITED",
            RejectMode.PortUnreachable => "PORT_UNREACHABLE",
            _ => throw new DomainInvariantException($"Unknown reject mode '{mode}'."),
        };

    /// <summary>
    /// Normative owner scope for non-exemption rule stages (Policy Model §13).
    /// Exemption stages are Exception-owned; use <see cref="IsOwnerEffectAllowed"/>.
    /// </summary>
    public static PolicyOwnerScope RequiredOwner(PolicyPipelineStage stage)
        => stage switch
        {
            PolicyPipelineStage.ProtectedControlPlane => PolicyOwnerScope.Company,
            PolicyPipelineStage.MandatoryPreStateDeny => PolicyOwnerScope.Company,
            PolicyPipelineStage.StatePrelude => PolicyOwnerScope.Company,
            PolicyPipelineStage.CompanyDeny => PolicyOwnerScope.Company,
            PolicyPipelineStage.SiteDeny => PolicyOwnerScope.Site,
            PolicyPipelineStage.NodeDeny => PolicyOwnerScope.Node,
            PolicyPipelineStage.CompanyAllow => PolicyOwnerScope.Company,
            PolicyPipelineStage.SiteAllow => PolicyOwnerScope.Site,
            PolicyPipelineStage.NodeAllow => PolicyOwnerScope.Node,
            PolicyPipelineStage.CompanyDenyExemptions
                or PolicyPipelineStage.SiteDenyExemptions
                or PolicyPipelineStage.NodeDenyExemptions
                => throw new DomainInvariantException(
                    $"{FormatStage(stage)} is Exception-owned; use IsOwnerEffectAllowed for placement checks."),
            PolicyPipelineStage.DefaultDisposition =>
                throw new DomainInvariantException("DEFAULT_DISPOSITION has no rule owner; it is driven by ChainContract."),
            _ => throw new DomainInvariantException($"Unknown pipeline stage '{stage}'."),
        };

    /// <summary>
    /// Whether a rule with the given owner/effect may be placed in the stage.
    /// Exemption stages require <see cref="PolicyKind.Exception"/>; other stages use owner_scope.
    /// </summary>
    public static bool IsOwnerEffectAllowed(
        PolicyPipelineStage stage,
        PolicyKind policyKind,
        PolicyOwnerScope ownerScope,
        PolicyRuleEffect effect)
    {
        if (stage == PolicyPipelineStage.DefaultDisposition)
        {
            return false;
        }

        if (!AllowedEffects(stage).Contains(effect))
        {
            return false;
        }

        return stage switch
        {
            PolicyPipelineStage.CompanyDenyExemptions
                or PolicyPipelineStage.SiteDenyExemptions
                or PolicyPipelineStage.NodeDenyExemptions
                => policyKind == PolicyKind.Exception
                   && effect == PolicyRuleEffect.ExemptDenyStage
                   && MatchesExemptionScope(stage, ownerScope),

            _ => policyKind != PolicyKind.Exception
                 && ownerScope == RequiredOwner(stage)
                 && MatchesPolicyKindOwner(policyKind, ownerScope),
        };
    }

    public static void EnsureOwnerEffectAllowed(
        PolicyPipelineStage stage,
        PolicyKind policyKind,
        PolicyOwnerScope ownerScope,
        PolicyRuleEffect effect)
    {
        if (IsOwnerEffectAllowed(stage, policyKind, ownerScope, effect))
        {
            return;
        }

        throw new DomainInvariantException(
            $"Forbidden pipeline placement: stage={FormatStage(stage)}, kind={PolicyCanonicalWriter.FormatKind(policyKind)}, " +
            $"owner={PolicyCanonicalWriter.FormatOwnerScope(ownerScope)}, effect={FormatEffect(effect)}.");
    }

    public static IReadOnlyList<PolicyRuleEffect> AllowedEffects(PolicyPipelineStage stage)
        => stage switch
        {
            PolicyPipelineStage.ProtectedControlPlane => [PolicyRuleEffect.Accept],
            PolicyPipelineStage.MandatoryPreStateDeny => [PolicyRuleEffect.Drop, PolicyRuleEffect.Reject],
            PolicyPipelineStage.StatePrelude =>
            [
                PolicyRuleEffect.Accept,
                PolicyRuleEffect.Drop,
                PolicyRuleEffect.FasttrackAccept,
            ],
            PolicyPipelineStage.CompanyDenyExemptions
                or PolicyPipelineStage.SiteDenyExemptions
                or PolicyPipelineStage.NodeDenyExemptions
                => [PolicyRuleEffect.ExemptDenyStage],
            PolicyPipelineStage.CompanyDeny
                or PolicyPipelineStage.SiteDeny
                or PolicyPipelineStage.NodeDeny
                => [PolicyRuleEffect.Drop, PolicyRuleEffect.Reject],
            PolicyPipelineStage.CompanyAllow
                or PolicyPipelineStage.SiteAllow
                or PolicyPipelineStage.NodeAllow
                => [PolicyRuleEffect.Accept],
            PolicyPipelineStage.DefaultDisposition => [],
            _ => throw new DomainInvariantException($"Unknown pipeline stage '{stage}'."),
        };

    /// <summary>
    /// Ensures <paramref name="effect"/> is in the normative allowed set for <paramref name="stage"/>
    /// (Policy Model §13). Does not check owner/kind placement — use <see cref="EnsureOwnerEffectAllowed"/>.
    /// </summary>
    public static void EnsureAllowedEffect(PolicyPipelineStage stage, PolicyRuleEffect effect)
    {
        if (AllowedEffects(stage).Contains(effect))
        {
            return;
        }

        throw new DomainInvariantException(
            $"Effect {FormatEffect(effect)} is not allowed in stage {FormatStage(stage)}.");
    }

    private static bool MatchesExemptionScope(PolicyPipelineStage stage, PolicyOwnerScope ownerScope)
        => stage switch
        {
            PolicyPipelineStage.CompanyDenyExemptions => ownerScope is PolicyOwnerScope.Site or PolicyOwnerScope.Node,
            PolicyPipelineStage.SiteDenyExemptions => ownerScope == PolicyOwnerScope.Site,
            PolicyPipelineStage.NodeDenyExemptions => ownerScope == PolicyOwnerScope.Node,
            _ => false,
        };

    private static bool MatchesPolicyKindOwner(PolicyKind kind, PolicyOwnerScope ownerScope)
        => kind switch
        {
            PolicyKind.CompanyBaseline => ownerScope == PolicyOwnerScope.Company,
            PolicyKind.SiteOverlay => ownerScope == PolicyOwnerScope.Site,
            PolicyKind.NodeOverlay => ownerScope == PolicyOwnerScope.Node,
            PolicyKind.Exception => false,
            _ => false,
        };
}
