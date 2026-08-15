using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps API-SSL discovery and filter discovery onto Domain management-path analysis (M2-13).
/// Includes dynamic filter rows and IP-service address restrictions that canonical sections may omit.
/// Does not create, move, or rewrite guards.
/// </summary>
public static class ManagementPathBlockerMapper
{
    public static ManagementPathAnalysisResult Analyze(
        ManagementAccessProfile profile,
        ApiSslServiceDiscovery apiSsl,
        FirewallFilterDiscoveryResult filter,
        IReadOnlyList<string>? candidateComments = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(apiSsl);
        ArgumentNullException.ThrowIfNull(filter);
        return ManagementPathAnalysis.Analyze(
            profile,
            FromApiSsl(apiSsl),
            ActualFilterRuleMapper.FromDiscovery(filter),
            candidateComments);
    }

    public static ManagementIpServiceFacts FromApiSsl(ApiSslServiceDiscovery apiSsl)
    {
        ArgumentNullException.ThrowIfNull(apiSsl);
        return ManagementIpServiceFacts.Create(
            apiSsl.Found,
            apiSsl.Disabled,
            apiSsl.Port,
            apiSsl.AddressPrefixes);
    }
}
