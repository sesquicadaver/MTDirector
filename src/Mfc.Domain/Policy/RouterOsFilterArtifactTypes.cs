using System.Collections.Immutable;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Built-in RouterOS filter chain context for an artifact chain (Compiler Spec §6).</summary>
public enum FilterBuiltInContext : byte
{
    Input = 0,
    Forward = 1,
    Output = 2,
}

/// <summary>Managed chain role within a family/built-in context (Compiler Spec §8 / M3-02 layout).</summary>
public enum FilterChainArtifactRole : byte
{
    Root = 0,
    CompanyDeny = 1,
    SiteDeny = 2,
    NodeDeny = 3,
}

/// <summary>One address-list entry in a filter artifact (no RouterOS <c>.id</c>, timeout, or commands).</summary>
public sealed class AddressListEntryArtifact : IEquatable<AddressListEntryArtifact>
{
    public string Address { get; }

    private AddressListEntryArtifact(string address) => Address = address;

    public static AddressListEntryArtifact Create(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new DomainInvariantException("Address-list entry address is required.");
        }

        string normalized = address.Trim();
        RouterOsFilterArtifactIdentity.EnsureNotForbiddenField("address", normalized);
        return new AddressListEntryArtifact(normalized);
    }

    public bool Equals(AddressListEntryArtifact? other)
        => other is not null && string.Equals(Address, other.Address, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AddressListEntryArtifact other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Address);
}

/// <summary>Content-addressed address-list resource (Compiler Spec §6 / §8.4).</summary>
public sealed class AddressListArtifact
{
    public IpAddressFamily Family { get; }

    public string Name { get; }

    public Hash256 ContentHash { get; }

    public ImmutableArray<AddressListEntryArtifact> Entries { get; }

    internal AddressListArtifact(
        IpAddressFamily family,
        string name,
        Hash256 contentHash,
        ImmutableArray<AddressListEntryArtifact> entries)
    {
        Family = family;
        Name = name;
        ContentHash = contentHash;
        Entries = entries;
    }
}

/// <summary>One desired filter rule inside a managed chain (Compiler Spec §6).</summary>
public sealed class FilterRuleArtifact
{
    public uint Ordinal { get; }

    public Guid? LogicalRuleId { get; }

    public uint? VariantIndex { get; }

    public string? StructuralRole { get; }

    public ImmutableSortedDictionary<string, string> Matchers { get; }

    public string Action { get; }

    public ImmutableSortedDictionary<string, string> ActionParameters { get; }

    public bool Log { get; }

    public string? LogPrefix { get; }

    public string Comment { get; }

    internal FilterRuleArtifact(
        uint ordinal,
        Guid? logicalRuleId,
        uint? variantIndex,
        string? structuralRole,
        ImmutableSortedDictionary<string, string> matchers,
        string action,
        ImmutableSortedDictionary<string, string> actionParameters,
        bool log,
        string? logPrefix,
        string comment)
    {
        Ordinal = ordinal;
        LogicalRuleId = logicalRuleId;
        VariantIndex = variantIndex;
        StructuralRole = structuralRole;
        Matchers = matchers;
        Action = action;
        ActionParameters = actionParameters;
        Log = log;
        LogPrefix = logPrefix;
        Comment = comment;
    }

    public static FilterRuleArtifact Create(
        uint ordinal,
        string action,
        string comment,
        IReadOnlyDictionary<string, string>? matchers = null,
        IReadOnlyDictionary<string, string>? actionParameters = null,
        Guid? logicalRuleId = null,
        uint? variantIndex = null,
        string? structuralRole = null,
        bool log = false,
        string? logPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainInvariantException("Filter rule action is required.");
        }

        RouterOsFilterArtifactIdentity.EnsureManagedComment(comment, "rule.comment");
        string normalizedAction = action.Trim();
        RouterOsFilterArtifactIdentity.EnsureNotApiCommand(normalizedAction);
        RouterOsFilterArtifactIdentity.EnsureNoRouterOsIdToken(normalizedAction);
        ImmutableSortedDictionary<string, string> normalizedMatchers =
            RouterOsFilterArtifactIdentity.NormalizePropertyMap(matchers, "matcher");
        ImmutableSortedDictionary<string, string> normalizedParams =
            RouterOsFilterArtifactIdentity.NormalizePropertyMap(actionParameters, "action_parameter");
        if (!string.IsNullOrWhiteSpace(logPrefix))
        {
            RouterOsFilterArtifactIdentity.EnsureNoRouterOsIdToken(logPrefix);
        }

        if (!string.IsNullOrWhiteSpace(structuralRole))
        {
            RouterOsFilterArtifactIdentity.EnsureNoRouterOsIdToken(structuralRole);
        }

        return new FilterRuleArtifact(
            ordinal,
            logicalRuleId,
            variantIndex,
            string.IsNullOrWhiteSpace(structuralRole) ? null : structuralRole.Trim(),
            normalizedMatchers,
            normalizedAction,
            normalizedParams,
            log,
            string.IsNullOrWhiteSpace(logPrefix) ? null : logPrefix.Trim(),
            comment.Trim());
    }
}

/// <summary>Managed chain resource (Compiler Spec §6).</summary>
public sealed class ChainArtifact
{
    public IpAddressFamily Family { get; }

    public FilterBuiltInContext BuiltInContext { get; }

    public string Name { get; }

    public FilterChainArtifactRole Role { get; }

    public ImmutableArray<FilterRuleArtifact> Rules { get; }

    internal ChainArtifact(
        IpAddressFamily family,
        FilterBuiltInContext builtInContext,
        string name,
        FilterChainArtifactRole role,
        ImmutableArray<FilterRuleArtifact> rules)
    {
        Family = family;
        BuiltInContext = builtInContext;
        Name = name;
        Role = role;
        Rules = rules;
    }
}

/// <summary>Desired anchor jump target (Compiler Spec §6 / §9). Compiler never creates the physical anchor.</summary>
public sealed class AnchorTargetArtifact : IEquatable<AnchorTargetArtifact>
{
    public IpAddressFamily Family { get; }

    public FilterBuiltInContext BuiltInChain { get; }

    public string ExpectedAnchorComment { get; }

    public string DesiredJumpTarget { get; }

    private AnchorTargetArtifact(
        IpAddressFamily family,
        FilterBuiltInContext builtInChain,
        string expectedAnchorComment,
        string desiredJumpTarget)
    {
        Family = family;
        BuiltInChain = builtInChain;
        ExpectedAnchorComment = expectedAnchorComment;
        DesiredJumpTarget = desiredJumpTarget;
    }

    public static AnchorTargetArtifact Create(
        IpAddressFamily family,
        FilterBuiltInContext builtInChain,
        string expectedAnchorComment,
        string desiredJumpTarget)
    {
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported anchor family '{family}'.");
        }

        RouterOsFilterArtifactIdentity.EnsureManagedComment(expectedAnchorComment, "expected_anchor_comment");
        if (string.IsNullOrWhiteSpace(desiredJumpTarget))
        {
            throw new DomainInvariantException("Desired jump target is required.");
        }

        string jump = desiredJumpTarget.Trim();
        RouterOsFilterArtifactIdentity.EnsureAsciiLowerResourceName(jump, "desired_jump_target");
        return new AnchorTargetArtifact(family, builtInChain, expectedAnchorComment.Trim(), jump);
    }

    public bool Equals(AnchorTargetArtifact? other)
        => other is not null
           && Family == other.Family
           && BuiltInChain == other.BuiltInChain
           && string.Equals(ExpectedAnchorComment, other.ExpectedAnchorComment, StringComparison.Ordinal)
           && string.Equals(DesiredJumpTarget, other.DesiredJumpTarget, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AnchorTargetArtifact other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Family, BuiltInChain, ExpectedAnchorComment, DesiredJumpTarget);
}

/// <summary>
/// Inputs that contribute to <c>physical_semantics_hash</c> (Compiler Spec §7.1).
/// Descriptions and timestamps are intentionally absent so description-only edits cannot change the artifact.
/// </summary>
public sealed class PhysicalSemanticsMaterial
{
    public required string LayoutVersion { get; init; }

    public required Hash256 CompilerProfileHash { get; init; }

    public required IReadOnlyList<Guid> RuleIds { get; init; }

    public required IReadOnlyList<string> ResolvedPredicateDigests { get; init; }

    public required IReadOnlyList<string> ResolvedZoneDigests { get; init; }

    public required IReadOnlyList<string> ActionDigests { get; init; }

    public required IReadOnlyList<string> LoggingDigests { get; init; }

    public required IReadOnlyList<string> ChainContractDigests { get; init; }
}
