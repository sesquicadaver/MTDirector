using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Topology;

/// <summary>
/// Validates declared node topology against explicit device observations (M1-18).
/// Never performs network discovery — facts must be supplied by the caller.
/// </summary>
public static class NodeTopologyValidator
{
    /// <summary>
    /// Validates <paramref name="node"/> against <paramref name="deviceFacts"/>.
    /// When every device cache is still valid for its capability hash, returns a cached pass without re-deriving findings.
    /// </summary>
    public static NodeTopologyValidationResult Validate(
        Node node,
        IReadOnlyList<DeviceTopologyFacts> deviceFacts,
        IReadOnlyDictionary<DeviceId, TopologyValidationCache>? capabilityCaches = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(deviceFacts);

        List<TopologyValidationFinding> findings = [];
        Dictionary<DeviceId, DeviceTopologyFacts> factsByDevice = IndexFacts(deviceFacts, findings);

        if (TryUseCapabilityCache(node, factsByDevice, capabilityCaches, out NodeTopologyValidationResult? cached))
        {
            return cached!;
        }

        ValidateBindings(node, factsByDevice, findings);
        ValidateCardinality(node, findings);
        ValidateBoardRoles(node, factsByDevice, findings);
        ObservedUplinkEvidence effective = ValidateUplinkMode(node, factsByDevice, findings);

        if (node.DeclaredKind == NodeKind.Vrrp)
        {
            ValidateVrrpGroups(node, factsByDevice, findings);
        }

        findings.Sort(static (a, b) =>
        {
            int byCode = string.CompareOrdinal(a.Code, b.Code);
            if (byCode != 0)
            {
                return byCode;
            }

            return string.CompareOrdinal(a.Subject ?? string.Empty, b.Subject ?? string.Empty);
        });

        bool isValid = findings.TrueForAll(static f => f.Severity != TopologyFindingSeverity.Blocker);

        if (isValid && capabilityCaches is not null)
        {
            RememberCaches(factsByDevice, capabilityCaches);
        }

        return new NodeTopologyValidationResult
        {
            NodeId = node.Id,
            IsValid = isValid,
            Findings = findings,
            EffectiveUplinkEvidence = effective,
            UsedCapabilityCache = false,
        };
    }

    private static Dictionary<DeviceId, DeviceTopologyFacts> IndexFacts(
        IReadOnlyList<DeviceTopologyFacts> deviceFacts,
        List<TopologyValidationFinding> findings)
    {
        Dictionary<DeviceId, DeviceTopologyFacts> map = new();
        foreach (DeviceTopologyFacts facts in deviceFacts)
        {
            ArgumentNullException.ThrowIfNull(facts);
            if (!map.TryAdd(facts.DeviceId, facts))
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.FactsDeviceUnknown,
                    Message = $"Duplicate topology facts for device '{facts.DeviceId}'.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = facts.DeviceId.ToString(),
                });
            }
        }

        return map;
    }

    private static bool TryUseCapabilityCache(
        Node node,
        IReadOnlyDictionary<DeviceId, DeviceTopologyFacts> factsByDevice,
        IReadOnlyDictionary<DeviceId, TopologyValidationCache>? capabilityCaches,
        out NodeTopologyValidationResult? result)
    {
        result = null;
        if (capabilityCaches is null || node.Devices.Count == 0)
        {
            return false;
        }

        foreach (Device device in node.Devices)
        {
            if (!factsByDevice.TryGetValue(device.Id, out DeviceTopologyFacts? facts)
                || facts.CapabilityHash is not { } capabilityHash
                || !capabilityCaches.TryGetValue(device.Id, out TopologyValidationCache? cache)
                || !cache.IsValidFor(capabilityHash))
            {
                return false;
            }
        }

        ObservedUplinkEvidence evidence = AggregateUplinkEvidence(factsByDevice.Values);
        result = new NodeTopologyValidationResult
        {
            NodeId = node.Id,
            IsValid = true,
            Findings = [],
            EffectiveUplinkEvidence = evidence,
            UsedCapabilityCache = true,
        };
        return true;
    }

    private static void RememberCaches(
        IReadOnlyDictionary<DeviceId, DeviceTopologyFacts> factsByDevice,
        IReadOnlyDictionary<DeviceId, TopologyValidationCache> capabilityCaches)
    {
        foreach ((DeviceId deviceId, DeviceTopologyFacts facts) in factsByDevice)
        {
            if (facts.CapabilityHash is { } hash
                && capabilityCaches.TryGetValue(deviceId, out TopologyValidationCache? cache))
            {
                cache.RememberValidated(hash);
            }
        }
    }

    private static void ValidateBindings(
        Node node,
        IReadOnlyDictionary<DeviceId, DeviceTopologyFacts> factsByDevice,
        List<TopologyValidationFinding> findings)
    {
        HashSet<DeviceId> nodeDevices = node.Devices.Select(static d => d.Id).ToHashSet();

        foreach (Device device in node.Devices)
        {
            if (!factsByDevice.TryGetValue(device.Id, out DeviceTopologyFacts? facts))
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.FactsDeviceUnknown,
                    Message = $"No topology facts supplied for bound device '{device.Id}'.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = device.Id.ToString(),
                });
                continue;
            }

            if (!facts.IsExplicitlyBoundToNode)
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.DeviceNotBoundToNode,
                    Message = $"Device '{device.Id}' must be explicitly bound to the node before topology validation.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = device.Id.ToString(),
                });
            }
        }

        foreach (DeviceId factsDeviceId in factsByDevice.Keys)
        {
            if (!nodeDevices.Contains(factsDeviceId))
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.FactsDeviceUnknown,
                    Message = $"Topology facts reference device '{factsDeviceId}' that is not bound to node '{node.Id}'.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = factsDeviceId.ToString(),
                });
            }
        }
    }

    private static void ValidateCardinality(Node node, List<TopologyValidationFinding> findings)
    {
        int count = node.Devices.Count;
        switch (node.DeclaredKind)
        {
            case NodeKind.Router when count >= 2:
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.RouterCardinalityViolation,
                    Message = $"ROUTER node rejects {count} devices; exactly one device is required.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = node.Id.ToString(),
                });
                break;
            case NodeKind.Switch when count >= 2:
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.SwitchCardinalityViolation,
                    Message = $"SWITCH node rejects {count} devices; exactly one device is required.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = node.Id.ToString(),
                });
                break;
            case NodeKind.Vrrp when count < 2:
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.VrrpCardinalityViolation,
                    Message = $"VRRP node rejects {count} device(s); at least two members are required.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = node.Id.ToString(),
                });
                break;
        }
    }

    private static void ValidateBoardRoles(
        Node node,
        Dictionary<DeviceId, DeviceTopologyFacts> factsByDevice,
        List<TopologyValidationFinding> findings)
    {
        foreach (Device device in node.Devices)
        {
            if (!factsByDevice.TryGetValue(device.Id, out DeviceTopologyFacts? facts))
            {
                continue;
            }

            if (facts.BoardRole == ObservedBoardRole.Unknown)
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.BoardRoleUncertain,
                    Message = $"Board role for device '{device.Id}' is uncertain; no silent assumption applied.",
                    Severity = TopologyFindingSeverity.Finding,
                    Subject = device.Id.ToString(),
                });
            }

            if (node.DeclaredKind == NodeKind.Switch && facts.GrantsTransitFirewallCapability)
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.SwitchTransitFirewallForbidden,
                    Message = $"SWITCH node device '{device.Id}' must not grant transit firewall capability.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = device.Id.ToString(),
                });
            }
        }
    }

    private static ObservedUplinkEvidence ValidateUplinkMode(
        Node node,
        IReadOnlyDictionary<DeviceId, DeviceTopologyFacts> factsByDevice,
        List<TopologyValidationFinding> findings)
    {
        if (node.DeclaredKind == NodeKind.Switch || node.DeclaredUplinkMode == DeclaredUplinkMode.None)
        {
            return ObservedUplinkEvidence.None;
        }

        ObservedUplinkEvidence aggregated = AggregateUplinkEvidence(factsByDevice.Values);

        // Interface count alone never classifies mode (AC#7–8).
        bool anyCountOnlyClaim = factsByDevice.Values.Any(static f =>
            f.UplinkEvidence == ObservedUplinkEvidence.Insufficient
            && f.ObservedUplinkInterfaceCount >= 2);

        if (aggregated == ObservedUplinkEvidence.Insufficient || anyCountOnlyClaim)
        {
            findings.Add(new TopologyValidationFinding
            {
                Code = TopologyValidationFinding.UplinkModeUncertain,
                Message =
                    "Uplink mode cannot be classified from routing/NAT/Mangle evidence; interface count alone is insufficient.",
                Severity = TopologyFindingSeverity.Finding,
                Subject = node.Id.ToString(),
            });
            return ObservedUplinkEvidence.Insufficient;
        }

        bool matches = node.DeclaredUplinkMode switch
        {
            DeclaredUplinkMode.One => aggregated is ObservedUplinkEvidence.SingleDefaultRoute
                or ObservedUplinkEvidence.None,
            DeclaredUplinkMode.Failover => aggregated == ObservedUplinkEvidence.FailoverDistanceRoutes,
            DeclaredUplinkMode.Balanced => aggregated == ObservedUplinkEvidence.BalancedPccOrEcmp,
            DeclaredUplinkMode.Mixed => aggregated == ObservedUplinkEvidence.Mixed,
            _ => true,
        };

        if (!matches)
        {
            findings.Add(new TopologyValidationFinding
            {
                Code = TopologyValidationFinding.UplinkModeEvidenceMismatch,
                Message =
                    $"Declared uplink mode '{node.DeclaredUplinkMode}' does not match observed evidence '{aggregated}'.",
                Severity = TopologyFindingSeverity.Blocker,
                Subject = node.Id.ToString(),
            });
        }

        return aggregated;
    }

    private static ObservedUplinkEvidence AggregateUplinkEvidence(IEnumerable<DeviceTopologyFacts> facts)
    {
        HashSet<ObservedUplinkEvidence> set = [];
        foreach (DeviceTopologyFacts f in facts)
        {
            if (f.UplinkEvidence != ObservedUplinkEvidence.None)
            {
                set.Add(f.UplinkEvidence);
            }
        }

        if (set.Count == 0)
        {
            return ObservedUplinkEvidence.None;
        }

        if (set.Contains(ObservedUplinkEvidence.Insufficient))
        {
            return ObservedUplinkEvidence.Insufficient;
        }

        if (set.Count == 1)
        {
            return set.First();
        }

        return ObservedUplinkEvidence.Mixed;
    }

    private static void ValidateVrrpGroups(
        Node node,
        Dictionary<DeviceId, DeviceTopologyFacts> factsByDevice,
        List<TopologyValidationFinding> findings)
    {
        // Group key: family + VRID (interface names may differ across members).
        Dictionary<(IpAddressFamily Family, byte Vrid), List<(DeviceId DeviceId, ObservedVrrpInstance Instance)>> byGroup =
            new();

        foreach (Device device in node.Devices)
        {
            if (!factsByDevice.TryGetValue(device.Id, out DeviceTopologyFacts? facts))
            {
                continue;
            }

            foreach (ObservedVrrpInstance instance in facts.VrrpInstances)
            {
                ArgumentNullException.ThrowIfNull(instance);
                (IpAddressFamily, byte) key = (instance.Family, instance.Vrid);
                if (!byGroup.TryGetValue(key, out List<(DeviceId, ObservedVrrpInstance)>? list))
                {
                    list = [];
                    byGroup[key] = list;
                }

                list.Add((device.Id, instance));
            }
        }

        IReadOnlyList<DeviceId> memberIds = node.Devices.Select(static d => d.Id).ToArray();

        foreach (((IpAddressFamily Family, byte Vrid) key, List<(DeviceId DeviceId, ObservedVrrpInstance Instance)> members) in byGroup
                     .OrderBy(static e => e.Key.Family)
                     .ThenBy(static e => e.Key.Vrid))
        {
            string subject = FormatGroupKey(key.Family, key.Vrid);

            HashSet<DeviceId> present = members.Select(static m => m.DeviceId).ToHashSet();
            foreach (DeviceId memberId in memberIds)
            {
                if (!present.Contains(memberId))
                {
                    findings.Add(new TopologyValidationFinding
                    {
                        Code = TopologyValidationFinding.VrrpGroupMembershipMismatch,
                        Message =
                            $"VRRP group {subject} is missing on member '{memberId}'.",
                        Severity = TopologyFindingSeverity.Blocker,
                        Subject = subject,
                    });
                }
            }

            HashSet<string> versions = members
                .Select(static m => NormalizeVersion(m.Instance.RouterOsVersion))
                .ToHashSet(StringComparer.Ordinal);
            if (versions.Count > 1)
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.VrrpVersionMismatch,
                    Message =
                        $"RouterOS version mismatch across members of VRRP group {subject}: {string.Join(", ", versions.Order(StringComparer.Ordinal))}.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = subject,
                });
            }

            int masterCount = members.Count(static m => m.Instance.ObservedState == VrrpMemberObservedState.Master);
            if (masterCount > 1)
            {
                findings.Add(new TopologyValidationFinding
                {
                    Code = TopologyValidationFinding.VrrpSplitMaster,
                    Message =
                        $"Split-master detected for VRRP group {subject}: {masterCount} members report Master.",
                    Severity = TopologyFindingSeverity.Blocker,
                    Subject = subject,
                });
            }
        }

        // Every VRRP member must participate in at least one shared group when cardinality is satisfied.
        if (node.Devices.Count >= 2 && byGroup.Count == 0)
        {
            findings.Add(new TopologyValidationFinding
            {
                Code = TopologyValidationFinding.VrrpGroupMembershipMismatch,
                Message = "VRRP node has no observed VRRP groups to compare across members.",
                Severity = TopologyFindingSeverity.Blocker,
                Subject = node.Id.ToString(),
            });
        }
    }

    private static string FormatGroupKey(IpAddressFamily family, byte vrid)
        => string.Create(CultureInfo.InvariantCulture, $"{family}/vrid-{vrid}");

    private static string NormalizeVersion(string version)
        => string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim();

    /// <summary>Stable fingerprint of findings for deterministic regression tests.</summary>
    public static string Fingerprint(NodeTopologyValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        StringBuilder sb = new();
        sb.Append(result.NodeId).Append('|')
            .Append(result.IsValid).Append('|')
            .Append(result.EffectiveUplinkEvidence).Append('|')
            .Append(result.UsedCapabilityCache).Append('|');
        foreach (TopologyValidationFinding f in result.Findings)
        {
            sb.Append(f.Code).Append(':')
                .Append(f.Severity).Append(':')
                .Append(f.Subject).Append(':')
                .Append(f.Message).Append(';');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }
}
