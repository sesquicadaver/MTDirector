using System.Collections.Immutable;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// One family/built-in surface plan for managed chain layout (Compiler Spec §11, M3-02).
/// Stage body ordinals are ignored and reassigned; empty deny bodies omit both chain and jump.
/// </summary>
public sealed class ManagedChainSurfacePlan
{
    public required IpAddressFamily Family { get; init; }

    public required FilterBuiltInContext BuiltInContext { get; init; }

    public required ChainDefaultDisposition DefaultDisposition { get; init; }

    /// <summary>Required when <see cref="DefaultDisposition"/> is <see cref="ChainDefaultDisposition.Reject"/>.</summary>
    public RejectMode? RejectModeValue { get; init; }

    public IReadOnlyList<FilterRuleArtifact> ProtectedControlPlane { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> IncidentPreStateDeny { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> MandatoryPreStateDeny { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> StatePrelude { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> CompanyDenyBody { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> SiteDenyBody { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> NodeDenyBody { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> CompanyAllow { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> SiteAllow { get; init; } = [];

    public IReadOnlyList<FilterRuleArtifact> NodeAllow { get; init; } = [];
}

/// <summary>Inputs for assembling a managed filter artifact layout (Compiler Spec §8 / §11).</summary>
public sealed class ManagedChainLayoutRequest
{
    public required Hash256 CompilerProfileHash { get; init; }

    public required Hash256 PhysicalSemanticsHash { get; init; }

    public required DeviceId DeviceId { get; init; }

    public required IReadOnlyList<ManagedChainSurfacePlan> Surfaces { get; init; }

    public IReadOnlyList<AddressListArtifactDraft> AddressLists { get; init; } = [];

    /// <summary>
    /// When true, emit desired anchor jump targets for each root (Compiler Spec §9).
    /// Physical anchors are never created.
    /// </summary>
    public bool EmitDesiredAnchorTargets { get; init; } = true;
}

/// <summary>
/// Builds root + deny-stage chains under <c>mfc4</c>/<c>mfc6</c> namespaces (M3-02).
/// Pure Domain layout: no RouterOS writes, no management-guard rules, no physical anchors.
/// Layout version is fixed to <see cref="ManagedChainNamespace.LayoutVersion"/>.
/// </summary>
public static class ManagedChainLayoutBuilder
{
    public const string JumpCompanyDenyComment = CompilerComments.JumpCompanyDeny;

    public const string JumpSiteDenyComment = CompilerComments.JumpSiteDeny;

    public const string JumpNodeDenyComment = CompilerComments.JumpNodeDeny;

    public const string ReturnCompanyDenyComment = CompilerComments.ReturnCompanyDeny;

    public const string ReturnSiteDenyComment = CompilerComments.ReturnSiteDeny;

    public const string ReturnNodeDenyComment = CompilerComments.ReturnNodeDeny;

    public const string TerminalComment = CompilerComments.Terminal;

    /// <summary>Assembles a sealed <see cref="RouterOsFilterArtifact"/> from surface plans.</summary>
    public static RouterOsFilterArtifact Build(ManagedChainLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CompilerProfileHash);
        ArgumentNullException.ThrowIfNull(request.PhysicalSemanticsHash);
        ArgumentNullException.ThrowIfNull(request.Surfaces);
        ArgumentNullException.ThrowIfNull(request.AddressLists);

        if (request.Surfaces.Count == 0)
        {
            throw new DomainInvariantException("Managed chain layout requires at least one surface.");
        }

        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(
            request.CompilerProfileHash,
            request.PhysicalSemanticsHash,
            request.DeviceId);

        HashSet<(IpAddressFamily Family, FilterBuiltInContext BuiltIn)> seen = [];
        List<ChainArtifactDraft> chains = [];
        List<AnchorTargetArtifact> anchors = [];

        foreach (ManagedChainSurfacePlan surface in request.Surfaces)
        {
            ArgumentNullException.ThrowIfNull(surface);
            ValidateSurface(surface);

            if (!seen.Add((surface.Family, surface.BuiltInContext)))
            {
                throw new DomainInvariantException(
                    "Managed chain layout allows only one root chain per family/built-in surface.");
            }

            BuildSurface(artifactId, surface, chains, anchors, request.EmitDesiredAnchorTargets);
        }

        return RouterOsFilterArtifact.Create(
            request.CompilerProfileHash,
            request.PhysicalSemanticsHash,
            request.DeviceId,
            request.AddressLists,
            chains,
            anchors,
            ManagedChainNamespace.LayoutVersion);
    }

    private static void ValidateSurface(ManagedChainSurfacePlan surface)
    {
        if (surface.Family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported layout family '{surface.Family}'.");
        }

        if (surface.BuiltInContext is not (
            FilterBuiltInContext.Input or FilterBuiltInContext.Forward or FilterBuiltInContext.Output))
        {
            throw new DomainInvariantException(
                $"Unsupported layout built-in context '{surface.BuiltInContext}'.");
        }

        switch (surface.DefaultDisposition)
        {
            case ChainDefaultDisposition.Drop:
                if (surface.RejectModeValue is not null)
                {
                    throw new DomainInvariantException("DROP terminal must not set reject_mode.");
                }

                break;

            case ChainDefaultDisposition.Reject:
                if (surface.RejectModeValue is null)
                {
                    throw new DomainInvariantException("REJECT terminal requires reject_mode.");
                }

                break;

            case ChainDefaultDisposition.ReturnToUnmanaged:
                if (surface.RejectModeValue is not null)
                {
                    throw new DomainInvariantException("RETURN_TO_UNMANAGED terminal must not set reject_mode.");
                }

                break;

            default:
                throw new DomainInvariantException(
                    "Default accept is impossible; use DROP, REJECT, or RETURN_TO_UNMANAGED.");
        }

        EnsureArtifactBoundary(surface.ProtectedControlPlane, "PROTECTED_CONTROL_PLANE");
        EnsureArtifactBoundary(surface.IncidentPreStateDeny, "INCIDENT_PRE_STATE_DENY");
        EnsureArtifactBoundary(surface.MandatoryPreStateDeny, "MANDATORY_PRE_STATE_DENY");
        EnsureArtifactBoundary(surface.StatePrelude, "STATE_PRELUDE");
        EnsureArtifactBoundary(surface.CompanyDenyBody, "COMPANY_DENY");
        EnsureArtifactBoundary(surface.SiteDenyBody, "SITE_DENY");
        EnsureArtifactBoundary(surface.NodeDenyBody, "NODE_DENY");
        EnsureArtifactBoundary(surface.CompanyAllow, "COMPANY_ALLOW");
        EnsureArtifactBoundary(surface.SiteAllow, "SITE_ALLOW");
        EnsureArtifactBoundary(surface.NodeAllow, "NODE_ALLOW");
    }

    private static void BuildSurface(
        string artifactId,
        ManagedChainSurfacePlan surface,
        List<ChainArtifactDraft> chains,
        List<AnchorTargetArtifact> anchors,
        bool emitDesiredAnchors)
    {
        bool hasCompanyDeny = HasBody(surface.CompanyDenyBody);
        bool hasSiteDeny = HasBody(surface.SiteDenyBody);
        bool hasNodeDeny = HasBody(surface.NodeDenyBody);

        string rootName = ManagedChainNamespace.ChainName(
            surface.Family,
            surface.BuiltInContext,
            FilterChainArtifactRole.Root,
            artifactId);

        List<FilterRuleArtifact> rootRules = [];
        AppendRelocated(rootRules, surface.ProtectedControlPlane);
        AppendRelocated(rootRules, surface.IncidentPreStateDeny);
        AppendRelocated(rootRules, surface.MandatoryPreStateDeny);
        AppendRelocated(rootRules, surface.StatePrelude);

        if (hasCompanyDeny)
        {
            string denyName = ManagedChainNamespace.ChainName(
                surface.Family,
                surface.BuiltInContext,
                FilterChainArtifactRole.CompanyDeny,
                artifactId);
            AppendJump(rootRules, denyName, JumpCompanyDenyComment, "jump:company-deny");
            chains.Add(BuildDenyChain(
                surface,
                FilterChainArtifactRole.CompanyDeny,
                denyName,
                surface.CompanyDenyBody,
                ReturnCompanyDenyComment,
                "return:company-deny"));
        }

        if (hasSiteDeny)
        {
            string denyName = ManagedChainNamespace.ChainName(
                surface.Family,
                surface.BuiltInContext,
                FilterChainArtifactRole.SiteDeny,
                artifactId);
            AppendJump(rootRules, denyName, JumpSiteDenyComment, "jump:site-deny");
            chains.Add(BuildDenyChain(
                surface,
                FilterChainArtifactRole.SiteDeny,
                denyName,
                surface.SiteDenyBody,
                ReturnSiteDenyComment,
                "return:site-deny"));
        }

        if (hasNodeDeny)
        {
            string denyName = ManagedChainNamespace.ChainName(
                surface.Family,
                surface.BuiltInContext,
                FilterChainArtifactRole.NodeDeny,
                artifactId);
            AppendJump(rootRules, denyName, JumpNodeDenyComment, "jump:node-deny");
            chains.Add(BuildDenyChain(
                surface,
                FilterChainArtifactRole.NodeDeny,
                denyName,
                surface.NodeDenyBody,
                ReturnNodeDenyComment,
                "return:node-deny"));
        }

        AppendRelocated(rootRules, surface.CompanyAllow);
        AppendRelocated(rootRules, surface.SiteAllow);
        AppendRelocated(rootRules, surface.NodeAllow);
        AppendTerminal(rootRules, surface);

        chains.Add(new ChainArtifactDraft
        {
            Family = surface.Family,
            BuiltInContext = surface.BuiltInContext,
            Name = rootName,
            Role = FilterChainArtifactRole.Root,
            Rules = rootRules,
        });

        if (emitDesiredAnchors)
        {
            anchors.Add(AnchorTargetArtifact.Create(
                surface.Family,
                surface.BuiltInContext,
                ManagedChainNamespace.DesiredAnchorComment(surface.Family, surface.BuiltInContext),
                rootName));
        }
    }

    private static ChainArtifactDraft BuildDenyChain(
        ManagedChainSurfacePlan surface,
        FilterChainArtifactRole role,
        string name,
        IReadOnlyList<FilterRuleArtifact> body,
        string returnComment,
        string returnStructuralRole)
    {
        List<FilterRuleArtifact> rules = [];
        AppendRelocated(rules, body);
        rules.Add(FilterRuleArtifact.Create(
            ordinal: (uint)rules.Count,
            action: "return",
            comment: returnComment,
            structuralRole: returnStructuralRole));

        return new ChainArtifactDraft
        {
            Family = surface.Family,
            BuiltInContext = surface.BuiltInContext,
            Name = name,
            Role = role,
            Rules = rules,
        };
    }

    private static void AppendJump(
        List<FilterRuleArtifact> rules,
        string jumpTarget,
        string comment,
        string structuralRole)
    {
        rules.Add(FilterRuleArtifact.Create(
            ordinal: (uint)rules.Count,
            action: "jump",
            comment: comment,
            structuralRole: structuralRole,
            matchers: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // RouterOS models jump-target alongside matchers (ActualFilterMatchers allowlist).
                ["jump-target"] = jumpTarget,
            }));
    }

    private static void AppendTerminal(List<FilterRuleArtifact> rules, ManagedChainSurfacePlan surface)
        => rules.Add(ChainTerminalCompiler.Compile(
            surface.DefaultDisposition,
            surface.RejectModeValue,
            ordinal: (uint)rules.Count));

    private static void AppendRelocated(List<FilterRuleArtifact> target, IReadOnlyList<FilterRuleArtifact> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (FilterRuleArtifact rule in source)
        {
            ArgumentNullException.ThrowIfNull(rule);
            EnsureArtifactBoundaryRule(rule);
            target.Add(Relocate(rule, (uint)target.Count));
        }
    }

    private static FilterRuleArtifact Relocate(FilterRuleArtifact source, uint ordinal)
        => FilterRuleArtifact.Create(
            ordinal,
            source.Action,
            source.Comment,
            matchers: source.Matchers,
            actionParameters: source.ActionParameters,
            logicalRuleId: source.LogicalRuleId,
            variantIndex: source.VariantIndex,
            structuralRole: source.StructuralRole,
            log: source.Log,
            logPrefix: source.LogPrefix);

    private static bool HasBody(IReadOnlyList<FilterRuleArtifact> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body.Count > 0;
    }

    private static void EnsureArtifactBoundary(IReadOnlyList<FilterRuleArtifact> rules, string stage)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (FilterRuleArtifact rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            EnsureArtifactBoundaryRule(rule, stage);
        }
    }

    private static void EnsureArtifactBoundaryRule(FilterRuleArtifact rule, string? stage = null)
    {
        if (ActualFilterMarker.IsGuard(rule.Comment))
        {
            string suffix = stage is null ? string.Empty : $" (stage {stage})";
            throw new DomainInvariantException(
                $"Management guard must not enter the filter artifact{suffix}.");
        }

        if (ActualFilterMarker.IsAnchor(rule.Comment))
        {
            string suffix = stage is null ? string.Empty : $" (stage {stage})";
            throw new DomainInvariantException(
                $"Physical anchor rules must not enter the filter artifact{suffix}; emit desired targets only.");
        }
    }
}
