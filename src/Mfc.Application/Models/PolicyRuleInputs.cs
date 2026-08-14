using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Models;

/// <summary>Input DTO for zone selectors on rule mutate commands.</summary>
public sealed class ZoneSelectorInput
{
    public IReadOnlyList<Guid> Include { get; init; } = [];

    public IReadOnlyList<Guid> Exclude { get; init; } = [];
}

/// <summary>Input DTO for address selectors on rule mutate commands.</summary>
public sealed class AddressSelectorInput
{
    public IReadOnlyList<Guid> Include { get; init; } = [];

    public IReadOnlyList<Guid> Exclude { get; init; } = [];
}

/// <summary>Input DTO for service selectors on rule mutate commands.</summary>
public sealed class ServiceSelectorInput
{
    public IReadOnlyList<Guid> Include { get; init; } = [];
}

/// <summary>Input DTO for TCP flag constraints.</summary>
public sealed class TcpFlagConstraintInput
{
    public IReadOnlyList<TcpHeaderBit> RequiredPresent { get; init; } = [];

    public IReadOnlyList<TcpHeaderBit> RequiredAbsent { get; init; } = [];
}

/// <summary>Input DTO for IPsec predicates.</summary>
public sealed class IpsecPolicyPredicateInput
{
    public required IpsecDirection Direction { get; init; }

    public required IpsecPolicyKind Policy { get; init; }
}

/// <summary>Input DTO for traffic predicates on rule mutate commands.</summary>
public sealed class TrafficPredicateInput
{
    public AddressSelectorInput? SourceAddresses { get; init; }

    public AddressSelectorInput? DestinationAddresses { get; init; }

    public ZoneSelectorInput? IngressZones { get; init; }

    public ZoneSelectorInput? EgressZones { get; init; }

    public ServiceSelectorInput? Services { get; init; }

    public IReadOnlyList<ConnectionState>? ConnectionStates { get; init; }

    public IReadOnlyList<ConnectionNatState>? ConnectionNatStates { get; init; }

    public IReadOnlyList<AddressType>? SourceAddressTypes { get; init; }

    public IReadOnlyList<AddressType>? DestinationAddressTypes { get; init; }

    public TcpFlagConstraintInput? TcpFlags { get; init; }

    public IpsecPolicyPredicateInput? IpsecPolicy { get; init; }
}

/// <summary>Input DTO for rule effects.</summary>
public sealed class RuleEffectInput
{
    public required PolicyRuleEffect Kind { get; init; }

    public RejectMode? RejectMode { get; init; }
}

/// <summary>Input DTO for rule logging.</summary>
public sealed class LogSpecificationInput
{
    public required bool Enabled { get; init; }

    public string? Prefix { get; init; }
}
