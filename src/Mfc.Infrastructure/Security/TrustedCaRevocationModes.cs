using System.Security.Cryptography.X509Certificates;

namespace Mfc.Infrastructure.Security;

/// <summary>Parses <see cref="TrustedCaStoreOptions.RevocationMode"/> for INTERNAL_CA (SEC-04).</summary>
public static class TrustedCaRevocationModes
{
    public static X509RevocationMode Parse(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return X509RevocationMode.Online;
        }

        if (string.Equals(configured.Trim(), "Online", StringComparison.OrdinalIgnoreCase))
        {
            return X509RevocationMode.Online;
        }

        if (string.Equals(configured.Trim(), "Offline", StringComparison.OrdinalIgnoreCase))
        {
            return X509RevocationMode.Offline;
        }

        if (string.Equals(configured.Trim(), "NoCheck", StringComparison.OrdinalIgnoreCase))
        {
            return X509RevocationMode.NoCheck;
        }

        throw new InvalidOperationException(
            $"Unknown Mfc:Security:TrustedCa:RevocationMode '{configured}'. Supported: Online, Offline, NoCheck.");
    }
}
