using Mfc.Domain.Inventory;

namespace Mfc.RouterOs.Discovery;

/// <summary>Derived VRRP role for one instance (Spec §33.3). Not a global device role.</summary>
public enum VrrpDerivedRole : byte
{
    Master = 0,
    Backup = 1,
    Initializing = 2,
    Inactive = 3,
    Failure = 4,
    Invalid = 5,
    Inconsistent = 6,
    Unknown = 7,
}

/// <summary>Identity of a Virtual Router: family + VRID + parent interface.</summary>
public readonly struct VrrpGroupKey : IEquatable<VrrpGroupKey>
{
    public VrrpGroupKey(IpAddressFamilyKind family, byte vrid, string interfaceName)
    {
        Family = family;
        Vrid = vrid;
        InterfaceName = interfaceName;
    }

    public IpAddressFamilyKind Family { get; }

    public byte Vrid { get; }

    public string InterfaceName { get; }

    public bool Equals(VrrpGroupKey other)
        => Family == other.Family
           && Vrid == other.Vrid
           && string.Equals(InterfaceName, other.InterfaceName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is VrrpGroupKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Family, Vrid, InterfaceName);

    public static bool operator ==(VrrpGroupKey left, VrrpGroupKey right) => left.Equals(right);

    public static bool operator !=(VrrpGroupKey left, VrrpGroupKey right) => !left.Equals(right);

    public override string ToString()
        => $"{Family}/vrid={Vrid}/if={InterfaceName}";
}

/// <summary>One discovered VRRP instance on the local device.</summary>
public sealed class VrrpInstanceDiscovery
{
    public required VrrpGroupKey GroupKey { get; init; }

    public required string? Name { get; init; }

    public required string? ParentInterface { get; init; }

    public required byte Vrid { get; init; }

    public required IpAddressFamilyKind Family { get; init; }

    public required byte? Priority { get; init; }

    /// <summary>True when configured priority is 255 (VRRP owner semantics).</summary>
    public required bool IsOwner { get; init; }

    public required string? Version { get; init; }

    public required string? V3Protocol { get; init; }

    public required string? Interval { get; init; }

    public required string? PreemptionMode { get; init; }

    /// <summary>Authentication mode only — password is never requested.</summary>
    public required string? AuthenticationMode { get; init; }

    public required string? Disabled { get; init; }

    /// <summary>Typed <c>sync-connection-tracking</c> (M2-14 AC#3). Configuration, not role.</summary>
    public string? SyncConnectionTracking { get; init; }

    public string? ConnectionTrackingMode { get; init; }

    public string? ConnectionTrackingPort { get; init; }

    public string? RemoteAddress { get; init; }

    public required string? Comment { get; init; }

    public required IReadOnlyList<string> VirtualAddresses { get; init; }

    public required VrrpDerivedRole ObservedRole { get; init; }

    /// <summary>Domain-facing observed state mapping (Init/Unknown absorb Failure/Invalid/Inactive).</summary>
    public required VrrpMemberObservedState DomainObservedState { get; init; }

    public required string? Running { get; init; }

    public required string? Master { get; init; }

    public required string? Backup { get; init; }

    public required string? Failure { get; init; }

    public required string? Invalid { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Aggregate VRRP discovery for one device (M1-15).</summary>
public sealed class VrrpDiscoveryResult
{
    public required IReadOnlyList<VrrpInstanceDiscovery> Instances { get; init; }

    public required IReadOnlyList<DiscoveryFinding> Findings { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// True when this device is MASTER for at least one VRID and BACKUP for another —
    /// split roles must not be collapsed to a single global master flag.
    /// </summary>
    public bool HasMixedMasterAndBackupRoles
        => Instances.Any(i => i.ObservedRole == VrrpDerivedRole.Master)
           && Instances.Any(i => i.ObservedRole == VrrpDerivedRole.Backup);

    /// <summary>Configuration hash material — excludes observed role / running / master flags.</summary>
    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (VrrpInstanceDiscovery instance in Instances
                         .OrderBy(i => i.GroupKey.ToString(), StringComparer.Ordinal))
            {
                string prefix = $"vrrp.{instance.GroupKey}";
                Put(material, $"{prefix}.name", instance.Name);
                Put(material, $"{prefix}.priority", instance.Priority?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Put(material, $"{prefix}.owner", instance.IsOwner ? "true" : "false");
                Put(material, $"{prefix}.version", instance.Version);
                Put(material, $"{prefix}.interval", instance.Interval);
                Put(material, $"{prefix}.preemption-mode", instance.PreemptionMode);
                Put(material, $"{prefix}.authentication", instance.AuthenticationMode);
                Put(material, $"{prefix}.disabled", instance.Disabled);
                Put(material, $"{prefix}.sync-connection-tracking", instance.SyncConnectionTracking);
                Put(material, $"{prefix}.connection-tracking-mode", instance.ConnectionTrackingMode);
                Put(material, $"{prefix}.connection-tracking-port", instance.ConnectionTrackingPort);
                Put(material, $"{prefix}.remote-address", instance.RemoteAddress);
                Put(
                    material,
                    $"{prefix}.addresses",
                    string.Join(',', instance.VirtualAddresses.OrderBy(a => a, StringComparer.Ordinal)));
            }

            return material;
        }
    }

    /// <summary>Observation hash material — role changes land here, not in configuration.</summary>
    public IReadOnlyDictionary<string, string> ObservationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            foreach (VrrpInstanceDiscovery instance in Instances
                         .OrderBy(i => i.GroupKey.ToString(), StringComparer.Ordinal))
            {
                string prefix = $"vrrp.{instance.GroupKey}";
                Put(material, $"{prefix}.role", instance.ObservedRole.ToString());
                Put(material, $"{prefix}.running", instance.Running);
                Put(material, $"{prefix}.master", instance.Master);
                Put(material, $"{prefix}.backup", instance.Backup);
                Put(material, $"{prefix}.failure", instance.Failure);
            }

            return material;
        }
    }

    private static void Put(Dictionary<string, string> target, string key, string? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }
}
