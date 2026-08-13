using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Observed interface row used for zone resolve (from latest capture).</summary>
public sealed class ZoneResolveInterfaceObservation
{
    public required string Name { get; init; }

    public required bool Dynamic { get; init; }
}

/// <summary>Per-device observation package for zone resolve.</summary>
public sealed class ZoneResolveDeviceObservation
{
    public required DeviceId DeviceId { get; init; }

    public required IReadOnlyList<ZoneResolveInterfaceObservation> Interfaces { get; init; }

    public required IReadOnlyList<InterfaceListSpec> InterfaceLists { get; init; }

    public required IReadOnlyList<InterfaceListMemberSpec> InterfaceListMembers { get; init; }

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
/// Pure per-Device zone binding resolve (Policy Model §21; M2-05 AC#3–9).
/// </summary>
public static class ZoneResolveEngine
{
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
        List<ZoneResolveBlocker> blockers = [];
        List<string> resolved = [];

        switch (binding.Kind)
        {
            case NodeZoneBindingKind.SingleInterface:
            case NodeZoneBindingKind.ExplicitInterfaceSet:
                foreach (string name in binding.Values)
                {
                    if (!interfaces.TryGetValue(name, out ZoneResolveInterfaceObservation? iface))
                    {
                        blockers.Add(new ZoneResolveBlocker
                        {
                            Code = ZoneResolveBlockerCodes.MissingInterface,
                            Message = $"Interface '{name}' does not exist on device.",
                            Subject = name,
                        });
                        continue;
                    }

                    if (iface.Dynamic)
                    {
                        blockers.Add(new ZoneResolveBlocker
                        {
                            Code = ZoneResolveBlockerCodes.DynamicInterface,
                            Message = $"Dynamic interface '{name}' is blocked for security zones.",
                            Subject = name,
                        });
                        continue;
                    }

                    resolved.Add(name);
                }

                break;

            case NodeZoneBindingKind.InterfaceList:
                {
                    string listName = binding.Values[0];
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
                        foreach (string name in match.Members)
                        {
                            if (!interfaces.TryGetValue(name, out ZoneResolveInterfaceObservation? iface))
                            {
                                blockers.Add(new ZoneResolveBlocker
                                {
                                    Code = ZoneResolveBlockerCodes.MissingInterface,
                                    Message = $"Resolved list member '{name}' does not exist on device.",
                                    Subject = name,
                                });
                                continue;
                            }

                            if (iface.Dynamic)
                            {
                                blockers.Add(new ZoneResolveBlocker
                                {
                                    Code = ZoneResolveBlockerCodes.DynamicInterface,
                                    Message = $"Dynamic interface '{name}' is blocked for security zones.",
                                    Subject = name,
                                });
                                continue;
                            }

                            resolved.Add(name);
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
}
