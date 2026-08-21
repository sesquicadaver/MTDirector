using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Routing;

/// <summary>
/// SHA-256 hash contracts for routing configuration vs operational material (M7.1-02).
/// Prefixes keep config and ops digests in distinct namespaces so equal payloads never collide.
/// </summary>
public static class RoutingAssuranceHashContract
{
    public const string ConfigurationPrefix = "mfc.routing.configuration.v1";
    public const string OperationalPrefix = "mfc.routing.operational.v1";

    /// <summary>Hashes ordered configuration key/value material.</summary>
    public static Hash256 HashConfiguration(IReadOnlyDictionary<string, string> material)
        => HashMaterial(ConfigurationPrefix, material);

    /// <summary>Hashes ordered operational key/value material.</summary>
    public static Hash256 HashOperational(IReadOnlyDictionary<string, string> material)
        => HashMaterial(OperationalPrefix, material);

    private static Hash256 HashMaterial(string prefix, IReadOnlyDictionary<string, string> material)
    {
        ArgumentNullException.ThrowIfNull(material);
        KeyValuePair<string, string>[] ordered = material
            .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
            .ToArray();

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, prefix);
        AppendNull(hasher);
        AppendUtf8(hasher, ordered.Length.ToString(CultureInfo.InvariantCulture));
        AppendNull(hasher);
        foreach (KeyValuePair<string, string> pair in ordered)
        {
            AppendUtf8(hasher, pair.Key);
            AppendNull(hasher);
            AppendUtf8(hasher, pair.Value);
            AppendNull(hasher);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendNull(IncrementalHash hasher)
        => hasher.AppendData([(byte)0]);
}
