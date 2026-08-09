using System.Globalization;
using Mfc.Domain.Canonicalization;

namespace Mfc.Domain.Diff;

/// <summary>
/// Diff-oriented view of a canonical record with extracted identity keys and fingerprint.
/// </summary>
public sealed class DiffRecordView
{
    public DiffRecordView(
        CanonicalRecord record,
        int index,
        string sectionId,
        DiffDomain domain)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        Record = record;
        Index = index;
        SectionId = sectionId;
        Domain = domain;
        Properties = record.Properties;
        Ordinal = ResolveOrdinal(Properties, index);
        FingerprintHex = RecordFingerprint.ComputeHex(Properties);
        if (Properties.TryGetValue("comment", out string? comment)
            && FwcRuleMarker.TryParse(comment, out FwcRuleMarker.ParsedMarker marker))
        {
            ControllerUuid = marker.Uuid;
        }

        NaturalKey = ResolveNaturalKey(sectionId, Properties);
        RecordKey = ControllerUuid is { } uuid
            ? FwcRuleMarker.FormatUuid(uuid)
            : NaturalKey ?? FingerprintHex;
    }

    public CanonicalRecord Record { get; }

    public int Index { get; }

    public string SectionId { get; }

    public DiffDomain Domain { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }

    public int Ordinal { get; }

    public string FingerprintHex { get; }

    public Guid? ControllerUuid { get; }

    public string? NaturalKey { get; }

    public string RecordKey { get; }

    private static int ResolveOrdinal(IReadOnlyDictionary<string, string> properties, int index)
    {
        if (properties.TryGetValue("ordinal", out string? raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ordinal)
            && ordinal >= 0)
        {
            return ordinal;
        }

        return index;
    }

    private static string? ResolveNaturalKey(string sectionId, IReadOnlyDictionary<string, string> properties)
    {
        if (string.Equals(sectionId, CanonicalSectionIds.NetworkInterfaces, StringComparison.Ordinal))
        {
            return GetProp(properties, "name");
        }

        if (string.Equals(sectionId, CanonicalSectionIds.HaVrrp, StringComparison.Ordinal))
        {
            return GetProp(properties, "group") ?? GetProp(properties, "name");
        }

        if (string.Equals(sectionId, CanonicalSectionIds.FirewallIpv4AddressLists, StringComparison.Ordinal)
            || string.Equals(sectionId, CanonicalSectionIds.FirewallIpv6AddressLists, StringComparison.Ordinal)
            || sectionId.EndsWith(".address-lists", StringComparison.Ordinal))
        {
            string? list = GetProp(properties, "list");
            string? address = GetProp(properties, "address");
            if (list is null || address is null)
            {
                return null;
            }

            return list + "|" + address;
        }

        if (string.Equals(sectionId, CanonicalSectionIds.NetworkInterfaceLists, StringComparison.Ordinal))
        {
            return GetProp(properties, "list") ?? GetProp(properties, "name");
        }

        if (string.Equals(sectionId, CanonicalSectionIds.RoutingTables, StringComparison.Ordinal))
        {
            return GetProp(properties, "name");
        }

        if (string.Equals(sectionId, CanonicalSectionIds.BridgeInstances, StringComparison.Ordinal))
        {
            return GetProp(properties, "name");
        }

        return null;
    }

    private static string? GetProp(IReadOnlyDictionary<string, string> properties, string name)
        => properties.TryGetValue(name, out string? value) && !string.IsNullOrEmpty(value) ? value : null;
}
