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
    /// Guard marker has a non-empty remainder. Strict <c>mfc:guard:v1:</c> form is also accepted;
    /// malformed empty <c>fwc:guard:</c>/<c>mfc:guard:</c> is not valid (Policy Model §46.1 #6).
    /// </summary>
    public static bool IsValidGuardMarker(string? comment)
    {
        if (!TryReadMarker(comment, out string? marker) || marker is null)
        {
            return false;
        }

        if (marker.StartsWith(FwcGuardPrefix, StringComparison.Ordinal))
        {
            return marker.Length > FwcGuardPrefix.Length;
        }

        if (marker.StartsWith(MfcGuardPrefix, StringComparison.Ordinal))
        {
            return marker.Length > MfcGuardPrefix.Length;
        }

        return false;
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
