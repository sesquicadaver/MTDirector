using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Fixed onboarding rollback script (Onboarding Spec §34). Literals are only exact
/// anchor comments and bootstrap root names — never operator/user text.
/// </summary>
public static class OnboardingWatchdogScript
{
    public const string Header = "# mfc.onboarding.watchdog.v1";

    public const string Policy = "read,write";

    public const string DontRequirePermissions = "no";

    /// <summary>
    /// True when a live rule may be disabled: unique marker, jump to the bootstrap root, currently enabled.
    /// Stale watchdog (target already a managed artifact) is a no-op.
    /// </summary>
    public static bool ShouldDisable(
        int matchCount,
        string? chain,
        string? action,
        string? jumpTarget,
        string expectedChain,
        string bootstrapRoot,
        bool disabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedChain);
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapRoot);
        if (matchCount != 1 || disabled)
        {
            return false;
        }

        if (!string.Equals(chain, expectedChain, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(action, "jump", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(jumpTarget, bootstrapRoot, StringComparison.Ordinal);
    }

    public static string Render(IReadOnlyList<AnchorKey> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        StringBuilder sb = new();
        sb.AppendLine(Header);
        foreach (AnchorKey key in anchors.OrderBy(static k => k.Marker, StringComparer.Ordinal))
        {
            string comment = key.Marker;
            string chain = BuiltinName(key.Chain);
            string root = BootstrapArtifact.RootChainName(key.Family, key.Chain);
            EnsureSafeLiteral(comment);
            EnsureSafeLiteral(chain);
            EnsureSafeLiteral(root);
            string tree = key.Family == IpAddressFamily.IPv4 ? "/ip firewall filter" : "/ipv6 firewall filter";
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{tree};"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $":local mfcN [:len [find where comment=\"{comment}\"]];"));
            sb.AppendLine(":if ($mfcN = 1) do={");
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  :local mfcId [find where comment=\"{comment}\"];"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  :if ([get $mfcId chain]=\"{chain}\" && [get $mfcId action]=\"jump\" && [get $mfcId jump-target]=\"{root}\") do={{"));
            sb.AppendLine("    :if ([get $mfcId disabled]=false) do={ set $mfcId disabled=yes }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
        }

        return sb.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static Hash256 HashSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static string BuiltinName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported watchdog chain '{chain}'."),
        };

    private static void EnsureSafeLiteral(string value)
    {
        foreach (char c in value)
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or ':' or '_')
            {
                continue;
            }

            throw new DomainInvariantException("Watchdog script literals must not contain user text.");
        }
    }
}
