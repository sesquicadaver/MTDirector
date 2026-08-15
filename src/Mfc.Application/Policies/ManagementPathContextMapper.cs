using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>
/// Maps canonical IP-service and filter records onto Domain management-path analysis (M2-13).
/// Does not call RouterOS and does not create or rewrite guards.
/// </summary>
public static class ManagementPathContextMapper
{
    public static ManagementPathAnalysisResult Analyze(
        ManagementAccessProfile profile,
        IReadOnlyList<CanonicalRecord> ipServices,
        IReadOnlyList<CanonicalRecord> ipv4Filter,
        IReadOnlyList<CanonicalRecord> ipv6Filter,
        IReadOnlyList<string>? candidateComments = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(ipServices);
        ArgumentNullException.ThrowIfNull(ipv4Filter);
        ArgumentNullException.ThrowIfNull(ipv6Filter);
        List<ActualFilterRule> rules = [];
        rules.AddRange(ActualFilterContextMapper.FromCanonicalFilter(IpAddressFamily.IPv4, ipv4Filter));
        rules.AddRange(ActualFilterContextMapper.FromCanonicalFilter(IpAddressFamily.IPv6, ipv6Filter));
        return ManagementPathAnalysis.Analyze(profile, FromCanonicalIpServices(ipServices), rules, candidateComments);
    }

    public static ManagementIpServiceFacts FromCanonicalIpServices(IReadOnlyList<CanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            return ManagementIpServiceFacts.Create(found: false, disabled: true, port: null, addressPrefixes: null);
        }

        IReadOnlyDictionary<string, string> properties = records[0].Properties;
        string? disabled = Get(properties, "api-ssl.disabled");
        bool found = disabled is not null || Get(properties, "api-ssl.port") is not null;
        return ManagementIpServiceFacts.Create(
            found,
            disabled: IsTruthy(disabled),
            port: Get(properties, "api-ssl.port"),
            addressPrefixes: Get(properties, "api-ssl.address"));
    }

    private static string? Get(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out string? value) ? value : null;

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
