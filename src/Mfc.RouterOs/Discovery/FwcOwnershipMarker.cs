namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Recognizes <c>fwc:</c> ownership markers in firewall comments without mutating device state (M1-13).
/// </summary>
public static class FwcOwnershipMarker
{
    public const string Prefix = "fwc:";

    /// <summary>
    /// Returns true when <paramref name="comment"/> contains an ownership marker.
    /// The marker is extracted as-is; discovery never rewrites comments or rules.
    /// </summary>
    public static bool TryRecognize(string? comment, out string? marker)
    {
        marker = null;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        int index = comment.IndexOf(Prefix, StringComparison.Ordinal);
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
        return marker.Length > Prefix.Length;
    }

    /// <summary>True when the rule is Controller-owned (recognized marker present).</summary>
    public static bool IsManaged(string? comment)
        => TryRecognize(comment, out _);
}
