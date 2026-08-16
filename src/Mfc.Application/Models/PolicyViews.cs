using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Models;

/// <summary>Soft/hard catalog warning surfaced on policy rule reads and mutations (M2-06 LOCK-5).</summary>
public sealed class PolicyWarningView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Application view of a zone selector.</summary>
public sealed class ZoneSelectorView
{
    public required IReadOnlyList<Guid> Include { get; init; }

    public required IReadOnlyList<Guid> Exclude { get; init; }
}

/// <summary>Application view of an address selector.</summary>
public sealed class AddressSelectorView
{
    public required IReadOnlyList<Guid> Include { get; init; }

    public required IReadOnlyList<Guid> Exclude { get; init; }
}

/// <summary>Application view of a service selector.</summary>
public sealed class ServiceSelectorView
{
    public required IReadOnlyList<Guid> Include { get; init; }
}

/// <summary>Application view of TCP flag constraints.</summary>
public sealed class TcpFlagConstraintView
{
    public required IReadOnlyList<TcpHeaderBit> RequiredPresent { get; init; }

    public required IReadOnlyList<TcpHeaderBit> RequiredAbsent { get; init; }
}

/// <summary>Application view of an IPsec predicate.</summary>
public sealed class IpsecPolicyPredicateView
{
    public required IpsecDirection Direction { get; init; }

    public required IpsecPolicyKind Policy { get; init; }
}

/// <summary>Application view of a traffic predicate.</summary>
public sealed class TrafficPredicateView
{
    public AddressSelectorView? SourceAddresses { get; init; }

    public AddressSelectorView? DestinationAddresses { get; init; }

    public ZoneSelectorView? IngressZones { get; init; }

    public ZoneSelectorView? EgressZones { get; init; }

    public ServiceSelectorView? Services { get; init; }

    public IReadOnlyList<ConnectionState>? ConnectionStates { get; init; }

    public IReadOnlyList<ConnectionNatState>? ConnectionNatStates { get; init; }

    public IReadOnlyList<AddressType>? SourceAddressTypes { get; init; }

    public IReadOnlyList<AddressType>? DestinationAddressTypes { get; init; }

    public TcpFlagConstraintView? TcpFlags { get; init; }

    public IpsecPolicyPredicateView? IpsecPolicy { get; init; }
}

/// <summary>Application view of a rule effect.</summary>
public sealed class RuleEffectView
{
    public required PolicyRuleEffect Kind { get; init; }

    public RejectMode? RejectMode { get; init; }
}

/// <summary>Application view of rule logging.</summary>
public sealed class LogSpecificationView
{
    public required bool Enabled { get; init; }

    public string? Prefix { get; init; }
}

/// <summary>Application view of a typed policy rule.</summary>
public sealed class PolicyRuleView
{
    public required Guid Id { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required PolicyPipelineStage Stage { get; init; }

    public required uint Ordinal { get; init; }

    public required bool Enabled { get; init; }

    public required TrafficPredicateView Predicate { get; init; }

    public required RuleEffectView Effect { get; init; }

    public required LogSpecificationView Logging { get; init; }

    public required bool ExceptionEligible { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<PolicyWarningView> Warnings { get; init; }
}

/// <summary>Application view of a policy revision document (rules + content hash).</summary>
public sealed class PolicyRevisionView
{
    public required Guid Id { get; init; }

    public required Guid PolicyId { get; init; }

    public required uint RevisionNumber { get; init; }

    public required uint SchemaVersion { get; init; }

    public required PolicyRevisionState State { get; init; }

    public required string ContentHashHex { get; init; }

    public string? ParentContextHashHex { get; init; }

    public required PolicyKind Kind { get; init; }

    public required PolicyOwnerScope OwnerScope { get; init; }

    public required IReadOnlyList<PolicyRuleView> Rules { get; init; }

    public required IReadOnlyList<PolicyWarningView> Warnings { get; init; }

    public ExceptionMetadataView? ExceptionMetadata { get; init; }

    public IReadOnlyList<AddressObjectView> AddressObjects { get; init; } = [];

    public IReadOnlyList<ServiceObjectView> ServiceObjects { get; init; } = [];

    public IReadOnlyList<ChainContractView> ChainContracts { get; init; } = [];

    /// <summary>Opaque residual tests array as a single JSON string (M2-18).</summary>
    public string TestsJson { get; init; } = "[]";
}

/// <summary>Wire/application view of one address catalog entry.</summary>
public sealed class AddressObjectEntryView
{
    public required string Kind { get; init; }

    public string? Address { get; init; }

    public byte? PrefixLength { get; init; }

    public string? Start { get; init; }

    public string? End { get; init; }
}

/// <summary>Wire/application view of an address object (M2-18).</summary>
public sealed class AddressObjectView
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required IReadOnlyList<AddressObjectEntryView> Entries { get; init; }

    public string? Description { get; init; }
}

/// <summary>Wire/application view of a port interval.</summary>
public sealed class PortIntervalView
{
    public required ushort Start { get; init; }

    public required ushort End { get; init; }
}

/// <summary>Wire/application view of an IP protocol matcher.</summary>
public sealed class IpProtocolView
{
    public required bool Any { get; init; }

    public byte? Number { get; init; }

    public string? CanonicalName { get; init; }
}

/// <summary>Wire/application view of an ICMP selector.</summary>
public sealed class IcmpSelectorView
{
    public required byte Type { get; init; }

    public byte? Code { get; init; }
}

/// <summary>Wire/application view of a service term.</summary>
public sealed class ServiceTermView
{
    public required IpProtocolView Protocol { get; init; }

    public IReadOnlyList<PortIntervalView> SourcePorts { get; init; } = [];

    public IReadOnlyList<PortIntervalView> DestinationPorts { get; init; } = [];

    public IReadOnlyList<IcmpSelectorView> IcmpSelectors { get; init; } = [];
}

/// <summary>Wire/application view of a service object (M2-18).</summary>
public sealed class ServiceObjectView
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ServiceTermView> Terms { get; init; }

    public string? Description { get; init; }
}

/// <summary>Wire/application view of a chain contract (M2-18).</summary>
public sealed class ChainContractView
{
    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required string DefaultDisposition { get; init; }

    public RejectMode? RejectMode { get; init; }
}

/// <summary>Semantic revision diff + risk summary (M2-18 review).</summary>
public sealed class PolicyRevisionDiffView
{
    public required Guid BeforeRevisionId { get; init; }

    public required Guid AfterRevisionId { get; init; }

    public required IReadOnlyList<PolicyRuleDiffLineView> RuleChanges { get; init; }

    public required IReadOnlyList<string> SemanticClasses { get; init; }

    public required IReadOnlyList<string> PacketSpaceClasses { get; init; }

    public required string RiskLevel { get; init; }

    public required IReadOnlyList<string> RiskDrivers { get; init; }

    public required IReadOnlyList<PolicyFindingSummaryView> FindingSummaries { get; init; }
}

/// <summary>One UUID-keyed rule change line.</summary>
public sealed class PolicyRuleDiffLineView
{
    public required Guid RuleId { get; init; }

    public required IReadOnlyList<string> Changes { get; init; }
}

/// <summary>Compact finding summary for review surfaces.</summary>
public sealed class PolicyFindingSummaryView
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }
}

/// <summary>Application view of typed exception metadata (M2-08).</summary>
public sealed class ExceptionMetadataView
{
    public required PolicyOwnerScope TargetScope { get; init; }

    public required Guid TargetScopeId { get; init; }

    public required PolicyPipelineStage TargetStage { get; init; }

    public required Guid WaivedRuleId { get; init; }

    public required DateTimeOffset ValidFrom { get; init; }

    public required DateTimeOffset ValidUntil { get; init; }

    public required string Reason { get; init; }

    public required string TicketReference { get; init; }

    public Guid? SupersedesExceptionId { get; init; }
}

/// <summary>Result of creating a policy + first draft revision.</summary>
public sealed class PolicyDraftView
{
    public required Guid PolicyId { get; init; }

    public required Guid RevisionId { get; init; }

    public required string Name { get; init; }

    public required PolicyKind Kind { get; init; }

    public required PolicyOwnerScope OwnerScope { get; init; }

    public Guid? OwnerId { get; init; }

    public required uint RevisionNumber { get; init; }

    public required string ContentHashHex { get; init; }
}

/// <summary>Mutation result carrying the new content hash and affected rule(s).</summary>
public sealed class PolicyRuleMutationView
{
    public required string ContentHashHex { get; init; }

    public PolicyRuleView? Rule { get; init; }

    public required IReadOnlyList<PolicyRuleView> Rules { get; init; }

    public required IReadOnlyList<PolicyWarningView> Warnings { get; init; }
}

/// <summary>List-rules response with revision content hash.</summary>
public sealed class PolicyRuleListView
{
    public required Guid RevisionId { get; init; }

    public required string ContentHashHex { get; init; }

    public required IReadOnlyList<PolicyRuleView> Rules { get; init; }

    public required IReadOnlyList<PolicyWarningView> Warnings { get; init; }
}

/// <summary>Loaded approved revision identity used in effective-policy refs (M2-07).</summary>
public sealed class PolicyRevisionRefView
{
    public required Guid PolicyId { get; init; }

    public required Guid RevisionId { get; init; }

    public required uint RevisionNumber { get; init; }

    public required byte[] ContentHash { get; init; }

    public required string ContentHashHex { get; init; }
}

/// <summary>Compute-on-read logical effective policy for a Node (M2-07).</summary>
public sealed class EffectivePolicyView
{
    public required Guid NodeId { get; init; }

    public required byte[] LogicalEffectiveHash { get; init; }

    public required string LogicalEffectiveHashHex { get; init; }

    public required PolicyRevisionRefView Company { get; init; }

    public PolicyRevisionRefView? Site { get; init; }

    public PolicyRevisionRefView? Node { get; init; }

    public required IReadOnlyList<PolicyRuleView> ActiveRules { get; init; }

    public required IReadOnlyList<PolicyWarningView> Findings { get; init; }
}

/// <summary>Captured analysis run used for approval (M2-17).</summary>
public sealed class PolicyAnalysisRunView
{
    public required Guid Id { get; init; }

    public required Guid RevisionId { get; init; }

    public required string BundleHashHex { get; init; }

    public required string DependencyFingerprintHex { get; init; }

    public required string RiskLevel { get; init; }

    public required string EffectiveRiskLevel { get; init; }

    public required bool EvidenceSignalsPresent { get; init; }
}

/// <summary>Result of recording a reviewer vote (M2-17). Binding is never created here.</summary>
public sealed class PolicyApprovalVoteView
{
    public required Guid ApprovalId { get; init; }

    public required Guid RevisionId { get; init; }

    public required PolicyRevisionState RevisionState { get; init; }

    public required bool CompletesApproval { get; init; }

    public required string BundleHashHex { get; init; }

    public required IReadOnlyList<Guid> BindingIds { get; init; }
}

/// <summary>Desired binding view. Activation does not start deployment (M2-17).</summary>
public sealed class PolicyBindingView
{
    public required Guid Id { get; init; }

    public required PolicyBindingScope Scope { get; init; }

    public Guid? ScopeId { get; init; }

    public required Guid PolicyId { get; init; }

    public required Guid DesiredRevisionId { get; init; }

    public required PolicyBindingState State { get; init; }

    public required ulong RowVersion { get; init; }

    public DateTimeOffset? ValidUntilUtc { get; init; }

    public required bool DeploymentStarted { get; init; }
}
