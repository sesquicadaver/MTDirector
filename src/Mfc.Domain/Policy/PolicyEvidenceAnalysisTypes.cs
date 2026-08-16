using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Policy test origin (Policy Model §54).</summary>
public enum PolicyTestOrigin : byte
{
    User = 0,
    System = 1,
}

/// <summary>Where the test packet is evaluated (Policy Model §54).</summary>
public enum PolicyTestExecutionMode : byte
{
    ManagedOnly = 0,
    NodeEffective = 1,
}

/// <summary>Expected terminal disposition for a <see cref="PolicyTestCase"/>.</summary>
public enum PolicyTestExpectedDisposition : byte
{
    Accept = 0,
    Drop = 1,
    Reject = 2,
    FasttrackAccept = 3,
    ReturnToUnmanaged = 4,
}

/// <summary>One hop on the matched evaluation path (Policy Model §57).</summary>
public enum PolicyTestPathKind : byte
{
    ManagementGuard = 0,
    UnmanagedRule = 1,
    ManagedStage = 2,
    ManagedRule = 3,
    ExceptionReturn = 4,
    DefaultDisposition = 5,
    PostAnchorRule = 6,
    BuiltInFallthrough = 7,
}

/// <summary>Concrete test packet (Policy Model §54 TestPacket). Not a live capture.</summary>
public sealed class PolicyTestPacket
{
    public required IpAddressFamily Family { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required string SourceAddress { get; init; }

    public required string DestinationAddress { get; init; }

    public byte? Protocol { get; init; }

    public ushort? SourcePort { get; init; }

    public ushort? DestinationPort { get; init; }

    public Guid? IngressZoneId { get; init; }

    public Guid? EgressZoneId { get; init; }

    public ConnectionState? ConnectionState { get; init; }

    public ConnectionNatState? ConnectionNatState { get; init; }

    public AddressType? SourceAddressType { get; init; }

    public AddressType? DestinationAddressType { get; init; }

    public TcpHeaderBit? TcpFlagPresent { get; init; }

    public byte? IcmpType { get; init; }

    public byte? IcmpCode { get; init; }

    public IpsecDirection? IpsecDirection { get; init; }

    public static PolicyTestPacket Create(
        IpAddressFamily family,
        PolicyFilterChain chain,
        string sourceAddress,
        string destinationAddress,
        byte? protocol = null,
        ushort? sourcePort = null,
        ushort? destinationPort = null,
        Guid? ingressZoneId = null,
        Guid? egressZoneId = null,
        ConnectionState? connectionState = null,
        ConnectionNatState? connectionNatState = null,
        AddressType? sourceAddressType = null,
        AddressType? destinationAddressType = null,
        TcpHeaderBit? tcpFlagPresent = null,
        byte? icmpType = null,
        byte? icmpCode = null,
        IpsecDirection? ipsecDirection = null)
    {
        if (string.IsNullOrWhiteSpace(sourceAddress) || string.IsNullOrWhiteSpace(destinationAddress))
        {
            throw new DomainInvariantException("Policy test packet requires source and destination addresses.");
        }

        return new PolicyTestPacket
        {
            Family = family,
            Chain = chain,
            SourceAddress = sourceAddress.Trim(),
            DestinationAddress = destinationAddress.Trim(),
            Protocol = protocol,
            SourcePort = sourcePort,
            DestinationPort = destinationPort,
            IngressZoneId = ingressZoneId,
            EgressZoneId = egressZoneId,
            ConnectionState = connectionState,
            ConnectionNatState = connectionNatState,
            SourceAddressType = sourceAddressType,
            DestinationAddressType = destinationAddressType,
            TcpFlagPresent = tcpFlagPresent,
            IcmpType = icmpType,
            IcmpCode = icmpCode,
            IpsecDirection = ipsecDirection,
        };
    }
}

/// <summary>Typed PolicyTestCase (Policy Model §54). SYSTEM tests cannot be disabled.</summary>
public sealed class PolicyTestCase
{
    public required PolicyTestId Id { get; init; }

    public required string Name { get; init; }

    public required PolicyTestOrigin Origin { get; init; }

    public required PolicyTestExecutionMode ExecutionMode { get; init; }

    public required PolicyTestPacket Packet { get; init; }

    public required PolicyTestExpectedDisposition Expected { get; init; }

    public RuleId? ExpectedRuleId { get; init; }

    public required bool Enabled { get; init; }

    public static PolicyTestCase Create(
        string name,
        PolicyTestOrigin origin,
        PolicyTestExecutionMode executionMode,
        PolicyTestPacket packet,
        PolicyTestExpectedDisposition expected,
        RuleId? expectedRuleId = null,
        bool enabled = true,
        PolicyTestId? id = null)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainInvariantException("Policy test name is required.");
        }

        if (origin == PolicyTestOrigin.System && !enabled)
        {
            throw new DomainInvariantException("SYSTEM policy tests cannot be disabled.");
        }

        return new PolicyTestCase
        {
            Id = id ?? PolicyTestId.New(),
            Name = name.Trim(),
            Origin = origin,
            ExecutionMode = executionMode,
            Packet = packet,
            Expected = expected,
            ExpectedRuleId = expectedRuleId,
            Enabled = enabled,
        };
    }
}

/// <summary>One hop recorded while evaluating a test packet.</summary>
public sealed class PolicyTestPathHop
{
    public required PolicyTestPathKind Kind { get; init; }

    public string? Subject { get; init; }

    public PolicyPipelineStage? Stage { get; init; }

    public RuleId? RuleId { get; init; }
}

/// <summary>Outcome of one <see cref="PolicyTestCase"/> (Policy Model §57).</summary>
public sealed class PolicyTestResult
{
    public required PolicyTestId TestId { get; init; }

    public required string Outcome { get; init; }

    public required IReadOnlyList<PolicyTestPathHop> MatchedPath { get; init; }

    public RuleId? MatchedRuleId { get; init; }

    public PolicyPipelineStage? MatchedStage { get; init; }

    public required PolicyTestExpectedDisposition FinalDisposition { get; init; }

    public required string Proof { get; init; }

    public string? FailureCode { get; init; }
}

/// <summary>UUID-keyed rule change. Fuzzy field matching is forbidden (Policy Model §61).</summary>
public sealed class PolicyRuleDiffEntry
{
    public required RuleId RuleId { get; init; }

    public required IReadOnlyList<string> Changes { get; init; }
}

/// <summary>Object identity whose content changed, with dependent managed rule ids.</summary>
public sealed class PolicyObjectImpact
{
    public required Guid ObjectId { get; init; }

    public required string ObjectKind { get; init; }

    public required IReadOnlyList<RuleId> DependentRuleIds { get; init; }
}

/// <summary>Semantic revision diff (Policy Model §61).</summary>
public sealed class PolicyRevisionDiffResult
{
    public required IReadOnlyList<PolicyRuleDiffEntry> RuleChanges { get; init; }

    public required IReadOnlyList<PolicyObjectImpact> ObjectImpacts { get; init; }

    public required IReadOnlyList<string> PacketSpaceClasses { get; init; }

    public required IReadOnlyList<string> SemanticClasses { get; init; }
}

/// <summary>Risk floor after mapping semantic classes and findings (Policy Model §60.2).</summary>
public sealed class PolicyRiskResult
{
    public required string Level { get; init; }

    public required IReadOnlyList<string> Drivers { get; init; }
}

/// <summary>One evidence finding. Target is test or rule UUID — not a live capture id.</summary>
public sealed class PolicyEvidenceFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public Guid? TargetId { get; init; }
}

/// <summary>Caller-supplied change flags that the rule diff cannot see (management path, exception metadata).</summary>
public sealed class PolicyEvidenceSignals
{
    public bool ManagementPathChanged { get; init; }

    public bool ExceptionChanged { get; init; }

    public bool DefaultDispositionChanged { get; init; }

    public bool ZoneBindingChanged { get; init; }

    public static PolicyEvidenceSignals None { get; } = new();
}

/// <summary>Outcome of <see cref="PolicyEvidenceAnalysis.Analyze"/>.</summary>
public sealed class PolicyEvidenceAnalysisResult
{
    public required IReadOnlyList<PolicyTestResult> TestResults { get; init; }

    public required PolicyRevisionDiffResult Diff { get; init; }

    public required PolicyRiskResult Risk { get; init; }

    public required IReadOnlyList<PolicyEvidenceFinding> Findings { get; init; }

    public required Hash256 EvidenceContextHash { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == PolicyEvidenceAnalysisCodes.SeverityBlocker);
}
