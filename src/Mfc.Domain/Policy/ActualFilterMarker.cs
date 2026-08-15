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

    /// <summary>True when the comment contains a controller ownership or layout marker.</summary>
    public static bool IsControllerOwned(string? comment)
        => TryReadMarker(comment, out _);

    /// <summary>Permanent jump-anchor that delimits unmanaged pre/post context.</summary>
    public static bool IsAnchor(string? comment)
        => TryReadMarker(comment, out string? marker)
           && marker is not null
           && (marker.StartsWith(FwcAnchorPrefix, StringComparison.Ordinal)
               || marker.StartsWith(MfcAnchorPrefix, StringComparison.Ordinal));

    /// <summary>Unmanaged means no valid <c>fwc:</c>/<c>mfc:</c> marker (MVP §12.2).</summary>
    public static bool IsUnmanaged(string? comment)
        => !IsControllerOwned(comment);

    /// <summary>Managed pipeline jump target (compiler namespace), not an unmanaged chain.</summary>
    public static bool IsManagedChainName(string? chain)
        => !string.IsNullOrWhiteSpace(chain)
           && (chain.StartsWith("fwc.", StringComparison.Ordinal)
               || chain.StartsWith("mfc.", StringComparison.Ordinal));

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
