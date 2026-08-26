using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Bounded stable-read capture for onboarding post-bootstrap filter equivalence (M5-07 AC#7).
/// </summary>
internal static class OnboardingFilterStableCapture
{
    public const string UnstableCode = "ONBOARDING_FILTER_UNSTABLE";

    public static async Task<IReadOnlyList<ActualFilterRule>> CaptureAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        FirewallFilterDiscoveryResult first = await FirewallFilterDiscovery.DiscoverAsync(session, cancellationToken)
            .ConfigureAwait(false);
        Hash256 before = DigestMaterial(first.ConfigurationHashMaterial);
        FirewallFilterDiscoveryResult second = await FirewallFilterDiscovery.DiscoverAsync(session, cancellationToken)
            .ConfigureAwait(false);
        Hash256 after = DigestMaterial(second.ConfigurationHashMaterial);
        if (!before.Equals(after))
        {
            throw new InvalidOperationException(
                $"{UnstableCode}: filter configuration changed between stable-read attempts.");
        }

        return ActualFilterRuleMapper.FromDiscovery(second);
    }

    private static Hash256 DigestMaterial(IReadOnlyDictionary<string, string> material)
    {
        ArgumentNullException.ThrowIfNull(material);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string key, string value) in material.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(key));
            hasher.AppendData([(byte)0]);
            hasher.AppendData(Encoding.UTF8.GetBytes(value));
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }
}
