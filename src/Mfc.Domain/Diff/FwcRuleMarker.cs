using System.Globalization;
using System.Text.RegularExpressions;

namespace Mfc.Domain.Diff;

/// <summary>
/// Parses strict <c>fwc:rule:</c> markers at the start of a comment (Canonical Spec §14).
/// </summary>
public static partial class FwcRuleMarker
{
    /// <summary>Successful parse of a controller-managed rule marker.</summary>
    public readonly record struct ParsedMarker(Guid Uuid, string RevisionToken);

    [GeneratedRegex(
        @"^fwc:rule:(?<uuid>[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}):(?<rev>[a-z0-9._-]{1,64})(?:\s|$)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex MarkerRegex();

    /// <summary>
    /// Tries to parse a managed-rule marker. Invalid or malformed comments are not managed.
    /// </summary>
    public static bool TryParse(string? comment, out ParsedMarker marker)
    {
        marker = default;
        if (string.IsNullOrEmpty(comment))
        {
            return false;
        }

        Match match = MarkerRegex().Match(comment);
        if (!match.Success)
        {
            return false;
        }

        if (!Guid.TryParseExact(match.Groups["uuid"].ValueSpan, "D", out Guid uuid))
        {
            return false;
        }

        string revision = match.Groups["rev"].Value;
        if (revision.Length is < 1 or > 64)
        {
            return false;
        }

        marker = new ParsedMarker(uuid, revision);
        return true;
    }

    /// <summary>Formats a marker UUID for record keys (lowercase D-format).</summary>
    public static string FormatUuid(Guid uuid)
        => uuid.ToString("D", CultureInfo.InvariantCulture);
}
