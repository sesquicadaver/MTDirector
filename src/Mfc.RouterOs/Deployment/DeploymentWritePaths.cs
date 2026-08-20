using Mfc.Domain.Inventory;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Compile-time allowlist of deployment write paths (Safe Deployment Spec §7).
/// <c>/move</c>, filter remove, address-list set/remove, and <c>system/script/run</c> are not members.
/// </summary>
public enum DeploymentWritePath : byte
{
    Ipv4AddressListAdd = 0,
    Ipv6AddressListAdd = 1,
    Ipv4FilterAdd = 2,
    Ipv6FilterAdd = 3,
    Ipv4FilterSet = 4,
    Ipv6FilterSet = 5,
    SystemScriptAdd = 6,
    SystemScriptRemove = 7,
    SystemSchedulerAdd = 8,
    SystemSchedulerSet = 9,
    SystemSchedulerRemove = 10,
    Ping = 11,
}

/// <summary>Print surfaces used for deployment lookup and read-back (Spec §8).</summary>
public enum DeploymentReadSurface : byte
{
    Ipv4Filter = 0,
    Ipv6Filter = 1,
    Ipv4AddressList = 2,
    Ipv6AddressList = 3,
    Script = 4,
    Scheduler = 5,
}

/// <summary>Maps <see cref="DeploymentWritePath"/> to fixed RouterOS API sentences.</summary>
public static class DeploymentWritePaths
{
    public static string Fixed(DeploymentWritePath path)
        => path switch
        {
            DeploymentWritePath.Ipv4AddressListAdd => "/ip/firewall/address-list/add",
            DeploymentWritePath.Ipv6AddressListAdd => "/ipv6/firewall/address-list/add",
            DeploymentWritePath.Ipv4FilterAdd => "/ip/firewall/filter/add",
            DeploymentWritePath.Ipv6FilterAdd => "/ipv6/firewall/filter/add",
            DeploymentWritePath.Ipv4FilterSet => "/ip/firewall/filter/set",
            DeploymentWritePath.Ipv6FilterSet => "/ipv6/firewall/filter/set",
            DeploymentWritePath.SystemScriptAdd => "/system/script/add",
            DeploymentWritePath.SystemScriptRemove => "/system/script/remove",
            DeploymentWritePath.SystemSchedulerAdd => "/system/scheduler/add",
            DeploymentWritePath.SystemSchedulerSet => "/system/scheduler/set",
            DeploymentWritePath.SystemSchedulerRemove => "/system/scheduler/remove",
            DeploymentWritePath.Ping => "/ping",
            _ => throw new InvalidOperationException($"Unsupported deployment write path '{path}'."),
        };

    public static DeploymentWritePath ForAddressListAdd(IpAddressFamily family)
        => family == IpAddressFamily.IPv4
            ? DeploymentWritePath.Ipv4AddressListAdd
            : DeploymentWritePath.Ipv6AddressListAdd;

    public static DeploymentWritePath ForFilterAdd(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? DeploymentWritePath.Ipv4FilterAdd : DeploymentWritePath.Ipv6FilterAdd;

    public static DeploymentWritePath ForFilterSet(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? DeploymentWritePath.Ipv4FilterSet : DeploymentWritePath.Ipv6FilterSet;

    public static bool IsFilterSet(DeploymentWritePath path)
        => path is DeploymentWritePath.Ipv4FilterSet or DeploymentWritePath.Ipv6FilterSet;

    public static bool IsAddressListAdd(DeploymentWritePath path)
        => path is DeploymentWritePath.Ipv4AddressListAdd or DeploymentWritePath.Ipv6AddressListAdd;
}

/// <summary>Transport used by the deployment session; tests substitute a recorder.</summary>
public interface IDeploymentWriteChannel
{
    Task<IReadOnlyDictionary<string, string>> SendAsync(
        DeploymentWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
        DeploymentReadSurface surface,
        CancellationToken cancellationToken = default);

    Task<ChannelPingResult> PingAsync(
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default);
}

/// <summary>Raw ping reply from the transport (mapped to <c>RouterPingResult</c> by the session).</summary>
public sealed class ChannelPingResult
{
    public required int Sent { get; init; }

    public required int Received { get; init; }

    public string? Detail { get; init; }
}
