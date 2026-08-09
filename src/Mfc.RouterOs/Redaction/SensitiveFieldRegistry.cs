namespace Mfc.RouterOs.Redaction;

/// <summary>
/// Centralized sensitive-field registry for RouterOS attribute names.
/// Used for logging redaction and to keep Forbidden properties out of request profiles.
/// </summary>
public static class SensitiveFieldRegistry
{
    private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "secret",
        "passphrase",
        "private-key",
        "private_key",
        "privatekey",
        "psk",
        "shared-key",
        "certificate-key",
        "auth-key",
        "user-password",
        "token",
        "access-token",
        "api-key",
        "apikey",
        // Container/App secrets and non-network payload (next-1 / N1-01).
        "env",
        "envs",
        "envlist",
        "mount",
        "mounts",
        "mountlists",
    };

    private static readonly HashSet<string> LogRedactedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "comment",
        "note",
        "serial-number",
    };

    /// <summary>Returns true when the attribute must never be requested or stored.</summary>
    public static bool IsForbidden(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        return ForbiddenNames.Contains(Normalize(attributeName));
    }

    /// <summary>Returns true when the value may be retained but must never appear in logs.</summary>
    public static bool IsLogRedacted(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        string name = Normalize(attributeName);
        return LogRedactedNames.Contains(name) || ForbiddenNames.Contains(name);
    }

    /// <summary>Redacts a value for logging according to the registry policy.</summary>
    public static string RedactForLog(string attributeName, string? value)
    {
        if (IsLogRedacted(attributeName))
        {
            return "[REDACTED]";
        }

        return value ?? string.Empty;
    }

    /// <summary>
    /// Removes forbidden attributes from a property bag for raw snapshot storage (M1-20).
    /// Unknown non-forbidden properties are preserved; order is ordinal by key.
    /// </summary>
    public static IReadOnlyDictionary<string, string> RedactForStorage(
        IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        SortedDictionary<string, string> kept = new(StringComparer.Ordinal);
        foreach ((string key, string value) in properties)
        {
            if (string.IsNullOrWhiteSpace(key) || IsForbidden(key))
            {
                continue;
            }

            kept[key] = value;
        }

        return kept;
    }

    private static string Normalize(string attributeName)
        => attributeName.StartsWith('=') ? attributeName[1..] : attributeName;
}
