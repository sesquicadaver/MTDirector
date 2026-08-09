using System.Security.Cryptography;
using System.Text;

namespace Mfc.Domain.Diff;

/// <summary>
/// Stable SHA-256 fingerprint over canonical properties excluding identity/ordinal keys.
/// </summary>
public static class RecordFingerprint
{
    /// <summary>
    /// Computes lowercase hex SHA-256 over sorted <c>name\0value\0</c> pairs,
    /// excluding <c>ordinal</c>, <c>.id</c>, <c>id</c>, and ordinal-like keys.
    /// </summary>
    public static string ComputeHex(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string name, string value) in properties.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            if (IsExcludedKey(name))
            {
                continue;
            }

            AppendNullTerminated(hasher, name);
            AppendNullTerminated(hasher, value);
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>True when the property must not participate in fingerprint material.</summary>
    public static bool IsExcludedKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (string.Equals(key, ".id", StringComparison.Ordinal)
            || string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "ordinal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Ordinal-like keys: "ordinal", "static-ordinal", "effective_ordinal", etc.
        return key.Contains("ordinal", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendNullTerminated(IncrementalHash hasher, string text)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        hasher.AppendData(utf8);
        hasher.AppendData([0]);
    }
}
