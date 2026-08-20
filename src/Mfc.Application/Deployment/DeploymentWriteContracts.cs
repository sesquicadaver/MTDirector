using System.Net;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Session-scoped RouterOS item id. Must not be persisted to PostgreSQL (Safe Deployment Spec §8).</summary>
public readonly record struct RouterOsItemId(string Value)
{
    public static RouterOsItemId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new RouterOsItemId(value.Trim());
    }

    public override string ToString() => Value;
}

/// <summary>Typed address-list add (Spec §7.1). No set/remove on this surface.</summary>
public sealed class AddressListEntryWrite
{
    public AddressListEntryWrite(
        IpAddressFamily family,
        string listName,
        string address,
        string? comment = null)
    {
        if (!Enum.IsDefined(family))
        {
            throw new DomainInvariantException($"Unknown address family '{family}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(listName);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        Family = family;
        ListName = listName.Trim();
        Address = address.Trim();
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    public IpAddressFamily Family { get; }

    public string ListName { get; }

    public string Address { get; }

    public string? Comment { get; }
}

/// <summary>Typed detached filter rule add (Spec §7.1). No remove/move/enable/disable here.</summary>
public sealed class FilterRuleWrite
{
    public FilterRuleWrite(
        IpAddressFamily family,
        string chain,
        string action,
        string? jumpTarget = null,
        string? comment = null,
        bool? disabled = null,
        IReadOnlyDictionary<string, string>? additionalMatchers = null)
    {
        if (!Enum.IsDefined(family))
        {
            throw new DomainInvariantException($"Unknown address family '{family}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(chain);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Family = family;
        Chain = chain.Trim();
        Action = action.Trim();
        JumpTarget = string.IsNullOrWhiteSpace(jumpTarget) ? null : jumpTarget.Trim();
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        Disabled = disabled;
        AdditionalMatchers = additionalMatchers ?? new Dictionary<string, string>(StringComparer.Ordinal);
        if (AdditionalMatchers.Keys.Any(static k =>
                k is ".id" or "move" or "place-before"
                || k.Contains("remove", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainInvariantException("FilterRuleWrite forbids .id/move/remove attributes on the typed write.");
        }
    }

    public IpAddressFamily Family { get; }

    public string Chain { get; }

    public string Action { get; }

    public string? JumpTarget { get; }

    public string? Comment { get; }

    public bool? Disabled { get; }

    public IReadOnlyDictionary<string, string> AdditionalMatchers { get; }
}

/// <summary>Typed permanent-anchor jump-target set (Spec §7.1). Only .id + jump-target on the wire.</summary>
public sealed class AnchorTargetWrite
{
    public AnchorTargetWrite(IpAddressFamily family, FilterBuiltInContext chain, string jumpTarget)
    {
        if (!Enum.IsDefined(family))
        {
            throw new DomainInvariantException($"Unknown address family '{family}'.");
        }

        if (chain is not (FilterBuiltInContext.Input or FilterBuiltInContext.Forward or FilterBuiltInContext.Output))
        {
            throw new DomainInvariantException($"Unsupported anchor chain '{chain}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(jumpTarget);
        Family = family;
        Chain = chain;
        JumpTarget = jumpTarget.Trim();
        OwnershipMarker = AnchorKey.Create(family, chain).Marker;
    }

    public IpAddressFamily Family { get; }

    public FilterBuiltInContext Chain { get; }

    public string JumpTarget { get; }

    public string OwnershipMarker { get; }
}

/// <summary>Typed rollback script add (Spec §7.2).</summary>
public sealed class RollbackScriptWrite
{
    public RollbackScriptWrite(string name, string source, Hash256 sourceHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(sourceHash);
        Name = name.Trim();
        Source = source;
        SourceHash = sourceHash;
    }

    public string Name { get; }

    public string Source { get; }

    public Hash256 SourceHash { get; }
}

/// <summary>Typed rollback scheduler add (Spec §7.2).</summary>
public sealed class RollbackSchedulerWrite
{
    public RollbackSchedulerWrite(
        string name,
        string onEvent,
        string? startTime = null,
        string? startDate = null,
        string? interval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(onEvent);
        Name = name.Trim();
        OnEvent = onEvent.Trim();
        StartTime = string.IsNullOrWhiteSpace(startTime) ? null : startTime.Trim();
        StartDate = string.IsNullOrWhiteSpace(startDate) ? null : startDate.Trim();
        Interval = string.IsNullOrWhiteSpace(interval) ? null : interval.Trim();
    }

    public string Name { get; }

    public string OnEvent { get; }

    public string? StartTime { get; }

    public string? StartDate { get; }

    public string? Interval { get; }
}

/// <summary>Bounded ICMP probe (Spec §33.2). Count fixed at 3; no DNS names.</summary>
public sealed class RouterPingRequest
{
    public const int FixedCount = 3;

    public const int MinTimeoutMs = 100;

    public const int MaxTimeoutMs = 5000;

    public RouterPingRequest(
        IPAddress destination,
        IpAddressFamily family,
        int timeoutMilliseconds = 1000,
        IPAddress? sourceAddress = null,
        string? routingTable = null,
        string? @interface = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!Enum.IsDefined(family))
        {
            throw new DomainInvariantException($"Unknown address family '{family}'.");
        }

        if (timeoutMilliseconds is < MinTimeoutMs or > MaxTimeoutMs)
        {
            throw new DomainInvariantException(
                $"Ping timeout must be between {MinTimeoutMs} and {MaxTimeoutMs} ms.");
        }

        Destination = destination;
        Family = family;
        TimeoutMilliseconds = timeoutMilliseconds;
        SourceAddress = sourceAddress;
        RoutingTable = string.IsNullOrWhiteSpace(routingTable) ? null : routingTable.Trim();
        Interface = string.IsNullOrWhiteSpace(@interface) ? null : @interface.Trim();
        Count = FixedCount;
    }

    public IPAddress Destination { get; }

    public IpAddressFamily Family { get; }

    public int TimeoutMilliseconds { get; }

    public IPAddress? SourceAddress { get; }

    public string? RoutingTable { get; }

    public string? Interface { get; }

    /// <summary>Always <see cref="FixedCount"/> (Spec §33.2).</summary>
    public int Count { get; }
}

/// <summary>Typed ping outcome (Spec §34).</summary>
public enum RouterPingOutcome : byte
{
    Pass = 0,
    Fail = 1,
    Inconclusive = 2,
    NotApplicable = 3,
}

public sealed class RouterPingResult
{
    public required RouterPingOutcome Outcome { get; init; }

    public required int Sent { get; init; }

    public required int Received { get; init; }

    public string? Detail { get; init; }
}

/// <summary>Read-only managed deployment state (Spec §6 ReadManagedStateAsync).</summary>
public sealed class ActualManagedState
{
    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Ipv4FilterRules { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Ipv6FilterRules { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Ipv4AddressLists { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Ipv6AddressLists { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Scripts { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Schedulers { get; init; }
}

/// <summary>One allowlisted mutation plus read-back (Spec §4.11 / §16).</summary>
public sealed class DeploymentWriteExecutionResult
{
    public required bool Succeeded { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> SentAttributes { get; init; }

    public required IReadOnlyDictionary<string, string> ReadBack { get; init; }

    public RouterOsItemId? SessionItemId { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Restricted deployment session (Safe Deployment Spec §6–§8 / M4-02).
/// No free-form command/menu/script/dictionary APIs. Namespace must not be Mfc.RouterOs.Write.
/// </summary>
public interface IRouterOsDeploymentSession : IAsyncDisposable
{
    Task<ActualManagedState> ReadManagedStateAsync(CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> AddAddressListEntryAsync(
        AddressListEntryWrite write,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> AddFilterRuleAsync(
        FilterRuleWrite write,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> AddRollbackScriptAsync(
        RollbackScriptWrite write,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> AddRollbackSchedulerAsync(
        RollbackSchedulerWrite write,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> DisableRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> RemoveRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken = default);

    Task<DeploymentWriteExecutionResult> RemoveRollbackScriptAsync(
        RouterOsItemId scriptId,
        CancellationToken cancellationToken = default);

    Task<RouterPingResult> PingAsync(
        RouterPingRequest request,
        CancellationToken cancellationToken = default);
}
