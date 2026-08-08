using System.Globalization;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Redaction;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Reads VRRP instances via the typed allowlist (M1-15).
/// Roles are per family+VRID+interface; password and transition scripts are never requested.
/// </summary>
public static class VrrpDiscovery
{
    public static async Task<VrrpDiscoveryResult> DiscoverAsync(
        RosSession session,
        InterfaceAddressDiscoveryResult? addressBindings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        List<string> warnings = [];
        RosReadCommandResult vrrp = await RosReadCommandExecutor.ExecuteAsync(
            session,
            RosReadCommandId.VrrpInterfaces,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!vrrp.IsSuccess)
        {
            warnings.Add($"VrrpInterfaces: {vrrp.Error?.Code} {vrrp.Error?.Message}");
        }

        return BuildResult(vrrp, addressBindings, warnings);
    }

    /// <summary>Builds discovery from an executed VRRP print plus optional address bindings.</summary>
    public static VrrpDiscoveryResult BuildResult(
        RosReadCommandResult vrrpInterfaces,
        InterfaceAddressDiscoveryResult? addressBindings = null,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(vrrpInterfaces);
        List<DiscoveryFinding> findings = [];
        Dictionary<string, List<string>> addressesByInterface = BuildAddressIndex(addressBindings);

        List<VrrpInstanceDiscovery> instances = [];
        foreach (RosReadRecord row in vrrpInterfaces.Records)
        {
            // Defense: never retain forbidden secrets even if returned.
            Dictionary<string, string> known = row.KnownProperties
                .Where(kv => !SensitiveFieldRegistry.IsForbidden(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            Dictionary<string, string> raw = row.RawProperties
                .Where(kv => !SensitiveFieldRegistry.IsForbidden(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

            string? name = Get(known, "name");
            string? parent = Get(known, "interface");
            string? vridRaw = Get(known, "vrid");
            if (!byte.TryParse(vridRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte vrid)
                || vrid == 0)
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.InvalidVrrpVrid,
                    Message = $"VRRP instance '{name}' has invalid VRID '{vridRaw}'.",
                    Subject = name,
                });
                continue;
            }

            byte? priority = ParseByte(Get(known, "priority"));
            bool isOwner = priority == 255;
            IpAddressFamilyKind family = ResolveFamily(Get(known, "v3-protocol"), name, addressesByInterface);
            string interfaceKey = parent ?? name ?? string.Empty;
            List<string> virtualAddresses = ResolveVirtualAddresses(name, family, addressesByInterface);

            VrrpDerivedRole role = DeriveRole(
                Get(known, "failure"),
                Get(known, "master"),
                Get(known, "backup"),
                Get(known, "invalid"),
                Get(known, "running"),
                out bool inconsistent);
            if (inconsistent)
            {
                findings.Add(new DiscoveryFinding
                {
                    Code = DiscoveryFinding.VrrpRoleInconsistent,
                    Message = $"VRRP instance '{name}' reports master and backup simultaneously.",
                    Subject = name,
                });
            }

            instances.Add(new VrrpInstanceDiscovery
            {
                GroupKey = new VrrpGroupKey(family, vrid, interfaceKey),
                Name = name,
                ParentInterface = parent,
                Vrid = vrid,
                Family = family,
                Priority = priority,
                IsOwner = isOwner,
                Version = Get(known, "version"),
                V3Protocol = Get(known, "v3-protocol"),
                Interval = Get(known, "interval"),
                PreemptionMode = Get(known, "preemption-mode"),
                AuthenticationMode = Get(known, "authentication"),
                Disabled = Get(known, "disabled"),
                Comment = Get(known, "comment"),
                VirtualAddresses = virtualAddresses
                    .OrderBy(a => a, StringComparer.Ordinal)
                    .ToArray(),
                ObservedRole = role,
                DomainObservedState = ToDomainState(role),
                Running = Get(known, "running"),
                Master = Get(known, "master"),
                Backup = Get(known, "backup"),
                Failure = Get(known, "failure"),
                Invalid = Get(known, "invalid"),
                RawProperties = raw,
            });
        }

        return new VrrpDiscoveryResult
        {
            Instances = instances
                .OrderBy(i => i.GroupKey.ToString(), StringComparer.Ordinal)
                .ToArray(),
            Findings = findings,
            Warnings = warnings?.ToArray() ?? [],
        };
    }

    public static VrrpDerivedRole DeriveRole(
        string? failure,
        string? master,
        string? backup,
        string? invalid,
        string? running,
        out bool inconsistent)
    {
        bool isFailure = IsTruthy(failure);
        bool isMaster = IsTruthy(master);
        bool isBackup = IsTruthy(backup);
        bool isInvalid = IsTruthy(invalid);
        bool isRunning = IsTruthy(running);
        inconsistent = isMaster && isBackup;

        if (inconsistent)
        {
            return VrrpDerivedRole.Inconsistent;
        }

        if (isFailure)
        {
            return VrrpDerivedRole.Failure;
        }

        if (isMaster)
        {
            return VrrpDerivedRole.Master;
        }

        if (isBackup)
        {
            return VrrpDerivedRole.Backup;
        }

        if (isInvalid)
        {
            return VrrpDerivedRole.Invalid;
        }

        if (isRunning)
        {
            return VrrpDerivedRole.Initializing;
        }

        return VrrpDerivedRole.Inactive;
    }

    private static VrrpMemberObservedState ToDomainState(VrrpDerivedRole role)
        => role switch
        {
            VrrpDerivedRole.Master => VrrpMemberObservedState.Master,
            VrrpDerivedRole.Backup => VrrpMemberObservedState.Backup,
            VrrpDerivedRole.Initializing => VrrpMemberObservedState.Init,
            _ => VrrpMemberObservedState.Unknown,
        };

    private static Dictionary<string, List<string>> BuildAddressIndex(
        InterfaceAddressDiscoveryResult? addressBindings)
    {
        Dictionary<string, List<string>> map = new(StringComparer.Ordinal);
        if (addressBindings is null)
        {
            return map;
        }

        foreach (IpAddressDiscovery address in addressBindings.Ipv4StaticAddresses
                     .Concat(addressBindings.Ipv4DynamicAddresses)
                     .Concat(addressBindings.Ipv6StaticAddresses)
                     .Concat(addressBindings.Ipv6DynamicAddresses))
        {
            if (string.IsNullOrWhiteSpace(address.Interface) || string.IsNullOrWhiteSpace(address.AddressCidr))
            {
                continue;
            }

            if (!map.TryGetValue(address.Interface, out List<string>? list))
            {
                list = [];
                map[address.Interface] = list;
            }

            list.Add(address.AddressCidr);
        }

        return map;
    }

    private static List<string> ResolveVirtualAddresses(
        string? vrrpName,
        IpAddressFamilyKind family,
        Dictionary<string, List<string>> addressesByInterface)
    {
        if (string.IsNullOrWhiteSpace(vrrpName)
            || !addressesByInterface.TryGetValue(vrrpName, out List<string>? addresses))
        {
            return [];
        }

        return addresses
            .Where(a => MatchesFamily(a, family))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IpAddressFamilyKind ResolveFamily(
        string? v3Protocol,
        string? vrrpName,
        Dictionary<string, List<string>> addressesByInterface)
    {
        if (string.Equals(v3Protocol, "ipv6", StringComparison.OrdinalIgnoreCase))
        {
            return IpAddressFamilyKind.Ipv6;
        }

        if (string.Equals(v3Protocol, "ipv4", StringComparison.OrdinalIgnoreCase))
        {
            return IpAddressFamilyKind.Ipv4;
        }

        if (!string.IsNullOrWhiteSpace(vrrpName)
            && addressesByInterface.TryGetValue(vrrpName, out List<string>? addresses))
        {
            if (addresses.Any(a => a.Contains(':', StringComparison.Ordinal)))
            {
                return IpAddressFamilyKind.Ipv6;
            }
        }

        return IpAddressFamilyKind.Ipv4;
    }

    private static bool MatchesFamily(string cidr, IpAddressFamilyKind family)
        => family == IpAddressFamilyKind.Ipv6
            ? cidr.Contains(':', StringComparison.Ordinal)
            : !cidr.Contains(':', StringComparison.Ordinal);

    private static byte? ParseByte(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte parsed)
            ? parsed
            : null;
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? Get(Dictionary<string, string> known, string name)
        => known.TryGetValue(name, out string? value) ? value : null;
}
