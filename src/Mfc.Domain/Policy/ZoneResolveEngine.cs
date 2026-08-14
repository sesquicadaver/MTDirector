using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Observed interface row used for zone resolve (from latest capture).</summary>
public sealed class ZoneResolveInterfaceObservation
{
    public required string Name { get; init; }

    public required bool Dynamic { get; init; }
}

/// <summary>
/// Domain-pure container/app → VETH membership edge (N1-05).
/// Plain strings only — no RouterOS types.
/// </summary>
public sealed class ZoneResolveContainerVethEdge
{
    /// <summary>Exact endpoint kind: <c>container</c> or <c>app</c>.</summary>
    public required string EndpointKind { get; init; }

    public required string EndpointName { get; init; }

    public required string VethName { get; init; }
}

/// <summary>Per-device observation package for zone resolve.</summary>
public sealed class ZoneResolveDeviceObservation
{
    public required DeviceId DeviceId { get; init; }

    public required IReadOnlyList<ZoneResolveInterfaceObservation> Interfaces { get; init; }

    public required IReadOnlyList<InterfaceListSpec> InterfaceLists { get; init; }

    public required IReadOnlyList<InterfaceListMemberSpec> InterfaceListMembers { get; init; }

    /// <summary>Container/app → VETH edges from canonical <c>topology.container-veth</c>.</summary>
    public IReadOnlyList<ZoneResolveContainerVethEdge> ContainerVethEdges { get; init; } = [];

    /// <summary>Shared VETH names from canonical <c>topology.shared-veth</c>.</summary>
    public IReadOnlyList<string> SharedVethNames { get; init; } = [];

    public required bool ObservationAvailable { get; init; }
}

/// <summary>Resolve outcome for one binding on one device.</summary>
public sealed class ZoneBindingResolveResult
{
    public required NodeZoneBindingId BindingId { get; init; }

    public required ZoneId ZoneId { get; init; }

    public required DeviceId DeviceId { get; init; }

    public required IReadOnlyList<string> ResolvedMembers { get; init; }

    public required Hash256 FreshDependencyHash { get; init; }

    public required bool AnalysisStale { get; init; }

    public required IReadOnlyList<ZoneResolveBlocker> Blockers { get; init; }
}

/// <summary>
/// Pure per-Device zone binding resolve (Policy Model §21; M2-05 AC#3–9; N1-05 markers).
/// </summary>
public static class ZoneResolveEngine
{
    public const string ContainerMarkerPrefix = "container:";
    public const string AppMarkerPrefix = "app:";
    public const string EndpointKindContainer = "container";
    public const string EndpointKindApp = "app";

    public static ZoneBindingResolveResult Resolve(
        NodeZoneBinding binding,
        ZoneResolveDeviceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(observation);

        if (!observation.ObservationAvailable)
        {
            Hash256 unavailableHash = NodeZoneBinding.ComputeDependencyHash(
                binding.Kind,
                binding.Values,
                resolvedMembers: []);
            return new ZoneBindingResolveResult
            {
                BindingId = binding.Id,
                ZoneId = binding.ZoneId,
                DeviceId = observation.DeviceId,
                ResolvedMembers = [],
                FreshDependencyHash = unavailableHash,
                AnalysisStale = !binding.ExpectedDependencyHash.Equals(unavailableHash),
                Blockers =
                [
                    new ZoneResolveBlocker
                    {
                        Code = ZoneResolveBlockerCodes.ObservationUnavailable,
                        Message = "Latest completed capture observation is unavailable for zone resolve.",
                        Subject = observation.DeviceId.ToString(),
                    },
                ],
            };
        }

        Dictionary<string, ZoneResolveInterfaceObservation> interfaces = observation.Interfaces
            .GroupBy(i => i.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        HashSet<string> known = new(interfaces.Keys, StringComparer.Ordinal);
        HashSet<string> sharedVeth = new(observation.SharedVethNames, StringComparer.Ordinal);
        List<ZoneResolveBlocker> blockers = [];
        List<string> resolved = [];

        switch (binding.Kind)
        {
            case NodeZoneBindingKind.SingleInterface:
            case NodeZoneBindingKind.ExplicitInterfaceSet:
                foreach (string name in binding.Values)
                {
                    ResolveInterfaceOrMarker(
                        name,
                        interfaces,
                        observation.ContainerVethEdges,
                        sharedVeth,
                        blockers,
                        resolved);
                }

                break;

            case NodeZoneBindingKind.InterfaceList:
                {
                    string listName = binding.Values[0];
                    if (TryParseEndpointMarker(listName, out _, out _))
                    {
                        blockers.Add(new ZoneResolveBlocker
                        {
                            Code = ZoneResolveBlockerCodes.MarkerNotAllowedOnInterfaceList,
                            Message =
                                $"Endpoint marker '{listName}' is not allowed as an interface-list binding value.",
                            Subject = listName,
                        });
                        break;
                    }

                    IReadOnlyList<ResolvedInterfaceListMembership> memberships = InterfaceListMembership.Resolve(
                        observation.InterfaceLists,
                        observation.InterfaceListMembers,
                        known,
                        out IReadOnlyList<InterfaceListMembershipFinding> findings);

                    foreach (InterfaceListMembershipFinding finding in findings)
                    {
                        if (finding.Code == InterfaceListMembershipFinding.InterfaceListCycle)
                        {
                            blockers.Add(new ZoneResolveBlocker
                            {
                                Code = ZoneResolveBlockerCodes.InterfaceListCycle,
                                Message = finding.Message,
                                Subject = finding.Subject,
                            });
                        }
                        else if (finding.Code == InterfaceListMembershipFinding.MissingListReference
                                 && string.Equals(finding.Subject, listName, StringComparison.Ordinal))
                        {
                            blockers.Add(new ZoneResolveBlocker
                            {
                                Code = ZoneResolveBlockerCodes.MissingInterfaceList,
                                Message = finding.Message,
                                Subject = finding.Subject,
                            });
                        }
                        else if (finding.Code == InterfaceListMembershipFinding.MissingInterfaceReference)
                        {
                            blockers.Add(new ZoneResolveBlocker
                            {
                                Code = ZoneResolveBlockerCodes.MissingInterface,
                                Message = finding.Message,
                                Subject = finding.Subject,
                            });
                        }
                    }

                    ResolvedInterfaceListMembership? match = memberships
                        .FirstOrDefault(m => string.Equals(m.ListName, listName, StringComparison.Ordinal));
                    if (match is null)
                    {
                        if (blockers.All(b => b.Code != ZoneResolveBlockerCodes.MissingInterfaceList))
                        {
                            blockers.Add(new ZoneResolveBlocker
                            {
                                Code = ZoneResolveBlockerCodes.MissingInterfaceList,
                                Message = $"Interface list '{listName}' does not exist on device.",
                                Subject = listName,
                            });
                        }
                    }
                    else
                    {
                        foreach (string memberName in match.Members)
                        {
                            TryAddResolvedInterface(memberName, interfaces, blockers, resolved);
                        }
                    }

                    break;
                }

            default:
                throw new DomainInvariantException($"Unknown binding kind '{binding.Kind}'.");
        }

        List<string> ordered = resolved
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        if (ordered.Count == 0
            && blockers.All(b => b.Code != ZoneResolveBlockerCodes.EmptyResolvedSet))
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = ZoneResolveBlockerCodes.EmptyResolvedSet,
                Message = "Resolved zone interface set is empty.",
                Subject = binding.ZoneId.ToString(),
            });
        }

        // LOCK-6: hash v1 over original values (including markers) + post-expansion members.
        Hash256 fresh = NodeZoneBinding.ComputeDependencyHash(binding.Kind, binding.Values, ordered);
        return new ZoneBindingResolveResult
        {
            BindingId = binding.Id,
            ZoneId = binding.ZoneId,
            DeviceId = observation.DeviceId,
            ResolvedMembers = ordered,
            FreshDependencyHash = fresh,
            AnalysisStale = !binding.ExpectedDependencyHash.Equals(fresh),
            Blockers = blockers,
        };
    }

    /// <summary>
    /// Parses exact <c>container:</c> / <c>app:</c> markers; name is trimmed remainder.
    /// </summary>
    public static bool TryParseEndpointMarker(string value, out string endpointKind, out string endpointName)
    {
        endpointKind = string.Empty;
        endpointName = string.Empty;
        if (value.StartsWith(ContainerMarkerPrefix, StringComparison.Ordinal))
        {
            endpointKind = EndpointKindContainer;
            endpointName = value[ContainerMarkerPrefix.Length..].Trim();
            return true;
        }

        if (value.StartsWith(AppMarkerPrefix, StringComparison.Ordinal))
        {
            endpointKind = EndpointKindApp;
            endpointName = value[AppMarkerPrefix.Length..].Trim();
            return true;
        }

        return false;
    }

    private static void ResolveInterfaceOrMarker(
        string value,
        Dictionary<string, ZoneResolveInterfaceObservation> interfaces,
        IReadOnlyList<ZoneResolveContainerVethEdge> edges,
        HashSet<string> sharedVeth,
        List<ZoneResolveBlocker> blockers,
        List<string> resolved)
    {
        if (!TryParseEndpointMarker(value, out string endpointKind, out string endpointName))
        {
            TryAddResolvedInterface(value, interfaces, blockers, resolved);
            return;
        }

        bool isContainer = string.Equals(endpointKind, EndpointKindContainer, StringComparison.Ordinal);
        string missingCode = isContainer
            ? ZoneResolveBlockerCodes.MissingContainer
            : ZoneResolveBlockerCodes.MissingApp;
        string unresolvedCode = isContainer
            ? ZoneResolveBlockerCodes.ContainerVethUnresolved
            : ZoneResolveBlockerCodes.AppVethUnresolved;
        string endpointLabel = isContainer ? "Container" : "App";

        if (string.IsNullOrEmpty(endpointName))
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = missingCode,
                Message = $"{endpointLabel} marker has an empty name.",
                Subject = value,
            });
            return;
        }

        List<string> vethNames = edges
            .Where(e =>
                string.Equals(e.EndpointKind, endpointKind, StringComparison.Ordinal)
                && string.Equals(e.EndpointName, endpointName, StringComparison.Ordinal))
            .Select(e => e.VethName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        bool hasAnyEdge = edges.Any(e =>
            string.Equals(e.EndpointKind, endpointKind, StringComparison.Ordinal)
            && string.Equals(e.EndpointName, endpointName, StringComparison.Ordinal));

        if (!hasAnyEdge)
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = missingCode,
                Message = $"{endpointLabel} '{endpointName}' is not present in topology observation.",
                Subject = value,
            });
            return;
        }

        if (vethNames.Count == 0)
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = unresolvedCode,
                Message = $"{endpointLabel} '{endpointName}' has no resolvable VETH membership.",
                Subject = value,
            });
            return;
        }

        int beforeCount = resolved.Count;
        foreach (string veth in vethNames)
        {
            if (sharedVeth.Contains(veth)
                && blockers.All(b =>
                    b.Code != ZoneResolveBlockerCodes.SharedVeth
                    || !string.Equals(b.Subject, veth, StringComparison.Ordinal)))
            {
                // LOCK-5: shared VETH is a blocker; members still resolve when otherwise valid.
                blockers.Add(new ZoneResolveBlocker
                {
                    Code = ZoneResolveBlockerCodes.SharedVeth,
                    Message =
                        $"VETH '{veth}' is shared by multiple containers/apps and must not be assumed 1:1.",
                    Subject = veth,
                });
            }

            TryAddResolvedInterface(veth, interfaces, blockers, resolved);
        }

        if (resolved.Count == beforeCount)
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = unresolvedCode,
                Message = $"{endpointLabel} '{endpointName}' expanded to VETH set that did not resolve.",
                Subject = value,
            });
        }
    }

    private static void TryAddResolvedInterface(
        string name,
        Dictionary<string, ZoneResolveInterfaceObservation> interfaces,
        List<ZoneResolveBlocker> blockers,
        List<string> resolved)
    {
        if (!interfaces.TryGetValue(name, out ZoneResolveInterfaceObservation? iface))
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = ZoneResolveBlockerCodes.MissingInterface,
                Message = $"Interface '{name}' does not exist on device.",
                Subject = name,
            });
            return;
        }

        if (iface.Dynamic)
        {
            blockers.Add(new ZoneResolveBlocker
            {
                Code = ZoneResolveBlockerCodes.DynamicInterface,
                Message = $"Dynamic interface '{name}' is blocked for security zones.",
                Subject = name,
            });
            return;
        }

        resolved.Add(name);
    }
}
