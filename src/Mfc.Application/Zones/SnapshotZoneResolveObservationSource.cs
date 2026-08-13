using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Zones;

/// <summary>
/// Builds <see cref="ZoneResolveDeviceObservation"/> from the device's latest completed capture
/// via <see cref="ISnapshotStore.LoadCanonicalSectionsAsync"/> (no RouterOS coupling).
/// </summary>
public sealed class SnapshotZoneResolveObservationSource : IZoneResolveObservationSource
{
    private readonly IDeviceStore _devices;
    private readonly ISnapshotStore _snapshots;

    public SnapshotZoneResolveObservationSource(IDeviceStore devices, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(snapshots);
        _devices = devices;
        _snapshots = snapshots;
    }

    public async Task<ZoneResolveDeviceObservation> GetForDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null || device.LastCompletedCaptureId is null)
        {
            return Unavailable(deviceId);
        }

        IReadOnlyList<CanonicalSection> sections = await _snapshots
            .LoadCanonicalSectionsAsync(new SnapshotId(device.LastCompletedCaptureId.Value), cancellationToken)
            .ConfigureAwait(false);
        if (sections.Count == 0)
        {
            return Unavailable(deviceId);
        }

        List<ZoneResolveInterfaceObservation> interfaces = ParseInterfaces(sections);
        (IReadOnlyList<InterfaceListSpec> lists, IReadOnlyList<InterfaceListMemberSpec> members) =
            ParseInterfaceLists(sections);

        // Observation is available when we have at least the interfaces section material,
        // even if the set is empty (empty → resolve blockers as designed).
        bool hasInterfaceSection = sections.Any(s =>
            string.Equals(s.SectionId, CanonicalSectionIds.NetworkInterfaces, StringComparison.Ordinal));
        if (!hasInterfaceSection)
        {
            return Unavailable(deviceId);
        }

        return new ZoneResolveDeviceObservation
        {
            DeviceId = deviceId,
            Interfaces = interfaces,
            InterfaceLists = lists,
            InterfaceListMembers = members,
            ObservationAvailable = true,
        };
    }

    private static ZoneResolveDeviceObservation Unavailable(DeviceId deviceId) => new()
    {
        DeviceId = deviceId,
        Interfaces = [],
        InterfaceLists = [],
        InterfaceListMembers = [],
        ObservationAvailable = false,
    };

    private static List<ZoneResolveInterfaceObservation> ParseInterfaces(IReadOnlyList<CanonicalSection> sections)
    {
        Dictionary<string, ZoneResolveInterfaceObservation> byName = new(StringComparer.Ordinal);
        foreach (CanonicalSection section in sections
                     .Where(s => string.Equals(s.SectionId, CanonicalSectionIds.NetworkInterfaces, StringComparison.Ordinal)))
        {
            foreach (CanonicalRecord record in section.Records)
            {
                if (!record.Properties.TryGetValue("name", out string? name) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                bool dynamic = IsTruthy(GetOptional(record, "dynamic"));
                // Prefer configuration domain when both exist; keep first dynamic=true if any record marks it.
                if (byName.TryGetValue(name, out ZoneResolveInterfaceObservation? existing))
                {
                    if (dynamic && !existing.Dynamic)
                    {
                        byName[name] = new ZoneResolveInterfaceObservation { Name = name, Dynamic = true };
                    }

                    continue;
                }

                byName[name] = new ZoneResolveInterfaceObservation { Name = name, Dynamic = dynamic };
            }
        }

        return byName.Values
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static (IReadOnlyList<InterfaceListSpec> Lists, IReadOnlyList<InterfaceListMemberSpec> Members)
        ParseInterfaceLists(IReadOnlyList<CanonicalSection> sections)
    {
        List<InterfaceListSpec> lists = [];
        List<InterfaceListMemberSpec> members = [];
        foreach (CanonicalSection section in sections
                     .Where(s => string.Equals(
                         s.SectionId,
                         CanonicalSectionIds.NetworkInterfaceLists,
                         StringComparison.Ordinal)
                         && s.Domain == CanonicalDomain.Configuration))
        {
            foreach (CanonicalRecord record in section.Records)
            {
                if (!record.Properties.TryGetValue("list", out string? listName)
                    || string.IsNullOrWhiteSpace(listName))
                {
                    continue;
                }

                // Canonical projector stores already-resolved membership as list+members CSV.
                // Reconstruct specs so Domain membership algorithm can re-apply membership lookup.
                lists.Add(new InterfaceListSpec
                {
                    Name = listName,
                    Include = [],
                    Exclude = [],
                });

                string membersCsv = GetOptional(record, "members") ?? string.Empty;
                foreach (string member in membersCsv.Split(
                             ',',
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    members.Add(new InterfaceListMemberSpec
                    {
                        List = listName,
                        Interface = member,
                        Disabled = false,
                    });
                }
            }
        }

        return (lists, members);
    }

    private static string? GetOptional(CanonicalRecord record, string key)
        => record.Properties.TryGetValue(key, out string? value) ? value : null;

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.Ordinal);
    }
}
