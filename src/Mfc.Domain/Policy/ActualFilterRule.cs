using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// One actual RouterOS filter rule for CFG analysis (Policy Model §44–§45).
/// Identity is family+chain+ordinal in the discovered order — not a managed UUID.
/// </summary>
public sealed class ActualFilterRule
{
    public required IpAddressFamily Family { get; init; }

    public required string Chain { get; init; }

    /// <summary>Effective order in the live chain, including dynamic rows.</summary>
    public required int Ordinal { get; init; }

    public required bool Disabled { get; init; }

    public required bool Dynamic { get; init; }

    public required string? Action { get; init; }

    public required string? JumpTarget { get; init; }

    public required string? Comment { get; init; }

    /// <summary>Allowlisted matchers (protocol, addresses, states, …).</summary>
    public required IReadOnlyDictionary<string, string> KnownMatchers { get; init; }

    /// <summary>Opaque or profile-unknown matchers; any hit is fail-closed indeterminate.</summary>
    public required IReadOnlyDictionary<string, string> UnknownMatchers { get; init; }

    public static ActualFilterRule Create(
        IpAddressFamily family,
        string chain,
        int ordinal,
        string? action,
        bool disabled = false,
        bool dynamic = false,
        string? jumpTarget = null,
        string? comment = null,
        IReadOnlyDictionary<string, string>? knownMatchers = null,
        IReadOnlyDictionary<string, string>? unknownMatchers = null)
    {
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported actual-filter family '{family}'.");
        }

        if (string.IsNullOrWhiteSpace(chain))
        {
            throw new DomainInvariantException("Actual-filter chain is required.");
        }

        if (ordinal < 0)
        {
            throw new DomainInvariantException("Actual-filter ordinal must be non-negative.");
        }

        return new ActualFilterRule
        {
            Family = family,
            Chain = chain.Trim(),
            Ordinal = ordinal,
            Disabled = disabled,
            Dynamic = dynamic,
            Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim(),
            JumpTarget = string.IsNullOrWhiteSpace(jumpTarget) ? null : jumpTarget.Trim(),
            Comment = comment,
            KnownMatchers = knownMatchers ?? new Dictionary<string, string>(StringComparer.Ordinal),
            UnknownMatchers = unknownMatchers ?? new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
