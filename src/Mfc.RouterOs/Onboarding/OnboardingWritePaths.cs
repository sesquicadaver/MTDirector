using Mfc.Domain.Inventory;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Compile-time allowlist of onboarding write paths (Onboarding Spec §27.1–§27.2).
/// Print/read-back stays on the existing read adapter; <c>/move</c> is not a member.
/// </summary>
public enum OnboardingWritePath : byte
{
    Ipv4FilterAdd = 0,
    Ipv6FilterAdd = 1,
    Ipv4FilterSet = 2,
    Ipv6FilterSet = 3,
    Ipv4FilterRemove = 4,
    Ipv6FilterRemove = 5,
    SystemScriptAdd = 6,
    SystemScriptRemove = 7,
    SystemSchedulerAdd = 8,
    SystemSchedulerSet = 9,
    SystemSchedulerRemove = 10,
}

/// <summary>Print surface for onboarding script/scheduler read-back (Spec §27.2).</summary>
public enum OnboardingSystemSurface : byte
{
    Script = 0,
    Scheduler = 1,
}

/// <summary>Maps <see cref="OnboardingWritePath"/> to fixed RouterOS API sentences.</summary>
public static class OnboardingWritePaths
{
    public static string Fixed(OnboardingWritePath path)
        => path switch
        {
            OnboardingWritePath.Ipv4FilterAdd => "/ip/firewall/filter/add",
            OnboardingWritePath.Ipv6FilterAdd => "/ipv6/firewall/filter/add",
            OnboardingWritePath.Ipv4FilterSet => "/ip/firewall/filter/set",
            OnboardingWritePath.Ipv6FilterSet => "/ipv6/firewall/filter/set",
            OnboardingWritePath.Ipv4FilterRemove => "/ip/firewall/filter/remove",
            OnboardingWritePath.Ipv6FilterRemove => "/ipv6/firewall/filter/remove",
            OnboardingWritePath.SystemScriptAdd => "/system/script/add",
            OnboardingWritePath.SystemScriptRemove => "/system/script/remove",
            OnboardingWritePath.SystemSchedulerAdd => "/system/scheduler/add",
            OnboardingWritePath.SystemSchedulerSet => "/system/scheduler/set",
            OnboardingWritePath.SystemSchedulerRemove => "/system/scheduler/remove",
            _ => throw new InvalidOperationException($"Unsupported onboarding write path '{path}'."),
        };

    public static OnboardingWritePath ForAdd(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? OnboardingWritePath.Ipv4FilterAdd : OnboardingWritePath.Ipv6FilterAdd;

    public static OnboardingWritePath ForSet(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? OnboardingWritePath.Ipv4FilterSet : OnboardingWritePath.Ipv6FilterSet;

    public static OnboardingWritePath ForRemove(IpAddressFamily family)
        => family == IpAddressFamily.IPv4 ? OnboardingWritePath.Ipv4FilterRemove : OnboardingWritePath.Ipv6FilterRemove;
}

/// <summary>Transport used by onboarding writers; tests substitute a recorder.</summary>
public interface IOnboardingWriteChannel
{
    Task<IReadOnlyDictionary<string, string>> SendAsync(
        OnboardingWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
        IpAddressFamily family,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintSystemAsync(
        OnboardingSystemSurface surface,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
}
