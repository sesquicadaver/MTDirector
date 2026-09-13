using System.Globalization;

namespace Mfc.Domain.Policy;

/// <summary>
/// Classifies RouterOS filter comments into anchor / controller-owned / unmanaged
/// (MVP §12, Policy Model §44). Does not move or rewrite rules.
/// </summary>
public static class ActualFilterMarker
{
    public const string FwcPrefix = "fwc:";

    public const string MfcPrefix = "mfc:";

    public const string FwcAnchorPrefix = "fwc:anchor:";

    public const string MfcAnchorPrefix = "mfc:anchor:";

    public const string FwcGuardPrefix = "fwc:guard:";

    public const string MfcGuardPrefix = "mfc:guard:";

    /// <summary>Strict Onboarding Spec §15 guard grammar prefix.</summary>
    public const string MfcGuardV1Prefix = "mfc:guard:v1:";

    /// <summary>True when the comment contains a controller ownership or layout marker.</summary>
    public static bool IsControllerOwned(string? comment)
        => TryReadMarker(comment, out _);

    /// <summary>Permanent jump-anchor that delimits unmanaged pre/post context.</summary>
    public static bool IsAnchor(string? comment)
        => TryReadMarker(comment, out string? marker)
           && marker is not null
           && (marker.StartsWith(FwcAnchorPrefix, StringComparison.Ordinal)
               || marker.StartsWith(MfcAnchorPrefix, StringComparison.Ordinal));

    /// <summary>Management-path guard (Onboarding §15; <c>fwc:guard:</c> or <c>mfc:guard:</c>).</summary>
    public static bool IsGuard(string? comment)
        => TryReadMarker(comment, out string? marker)
           && marker is not null
           && (marker.StartsWith(FwcGuardPrefix, StringComparison.Ordinal)
               || marker.StartsWith(MfcGuardPrefix, StringComparison.Ordinal));

    /// <summary>
    /// Guard marker is a strict Spec §15 token:
    /// <c>mfc:guard:v1:&lt;16-hex-id&gt;:{4|6}:{i|o}:&lt;ordinal&gt;</c> at the start of the comment.
    /// Legacy <c>fwc:guard:*</c> / arbitrary <c>mfc:guard:</c> suffixes are ownership markers but not valid.
    /// </summary>
    public static bool IsValidGuardMarker(string? comment)
        => TryParseStrictGuardMarker(comment, out _, out _, out _, out _);

    /// <summary>
    /// Parses a strict Spec §15 guard marker that occupies the first token of <paramref name="comment"/>.
    /// Shared by ManagementPath and Onboarding <c>GuardMarker</c> (Policy → no Onboarding dependency).
    /// </summary>
    public static bool TryParseStrictGuardMarker(
        string? comment,
        out string profileIdHex,
        out char familyCode,
        out char directionCode,
        out int ordinal)
    {
        profileIdHex = string.Empty;
        familyCode = '\0';
        directionCode = '\0';
        ordinal = 0;
        if (string.IsNullOrWhiteSpace(comment)
            || !comment.StartsWith(MfcGuardV1Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryReadMarker(comment, out string? marker)
            || marker is null
            || !comment.StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        // mfc:guard:v1:<id>:<4|6>:<i|o>:<ordinal>
        string[] parts = marker.Split(':');
        if (parts.Length != 7
            || !string.Equals(parts[0], "mfc", StringComparison.Ordinal)
            || !string.Equals(parts[1], "guard", StringComparison.Ordinal)
            || !string.Equals(parts[2], "v1", StringComparison.Ordinal))
        {
            return false;
        }

        if (!IsGuardProfileIdHex(parts[3]))
        {
            return false;
        }

        if (parts[4] is not ("4" or "6"))
        {
            return false;
        }

        if (parts[5] is not ("i" or "o"))
        {
            return false;
        }

        if (!int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out ordinal)
            || ordinal < 0)
        {
            return false;
        }

        profileIdHex = parts[3];
        familyCode = parts[4][0];
        directionCode = parts[5][0];
        return true;
    }

    /// <summary>Unmanaged means no valid <c>fwc:</c>/<c>mfc:</c> marker (MVP §12.2).</summary>
    public static bool IsUnmanaged(string? comment)
        => !IsControllerOwned(comment);

    /// <summary>
    /// Managed pipeline jump target (compiler namespace), not an unmanaged chain.
    /// Compiler Spec §8.3: <c>mfc{4|6}.{i|f|o}.{r|dc|ds|dn}.&lt;artifact-id&gt;</c>;
    /// also accepts legacy <c>mfc.*</c> / <c>fwc.*</c>. Address-list names (<c>mfc4.a.*</c>) are not chains.
    /// </summary>
    public static bool IsManagedChainName(string? chain)
    {
        if (string.IsNullOrWhiteSpace(chain))
        {
            return false;
        }

        if (chain.StartsWith("fwc.", StringComparison.Ordinal)
            || chain.StartsWith("mfc.", StringComparison.Ordinal))
        {
            return true;
        }

        string[] parts = chain.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        if (parts[0] is not (ManagedChainNamespace.Ipv4Prefix or ManagedChainNamespace.Ipv6Prefix))
        {
            return false;
        }

        if (parts[1] is not ("i" or "f" or "o"))
        {
            return false;
        }

        if (parts[2] is not ("r" or "dc" or "ds" or "dn"))
        {
            return false;
        }

        return IsCompilerArtifactIdToken(parts[3]);
    }

    private static bool IsGuardProfileIdHex(string token)
    {
        if (token.Length != 16)
        {
            return false;
        }

        foreach (char c in token)
        {
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsCompilerArtifactIdToken(string token)
    {
        if (token.Length != RouterOsFilterArtifactIdentity.ArtifactIdHexLength)
        {
            return false;
        }

        foreach (char c in token)
        {
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static bool TryReadMarker(string? comment, out string? marker)
    {
        marker = null;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        int fwc = comment.IndexOf(FwcPrefix, StringComparison.Ordinal);
        int mfc = comment.IndexOf(MfcPrefix, StringComparison.Ordinal);
        int index;
        if (fwc < 0)
        {
            index = mfc;
        }
        else if (mfc < 0)
        {
            index = fwc;
        }
        else
        {
            index = Math.Min(fwc, mfc);
        }

        if (index < 0)
        {
            return false;
        }

        int end = index;
        while (end < comment.Length)
        {
            char c = comment[end];
            if (char.IsWhiteSpace(c) || c is ',' or ';')
            {
                break;
            }

            end++;
        }

        marker = comment[index..end];
        return marker.Length > FwcPrefix.Length;
    }
}
