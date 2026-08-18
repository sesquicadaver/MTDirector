using System.Globalization;

namespace Mfc.Domain.Policy;

/// <summary>
/// Deterministic managed comments (Compiler Spec §23 / §27). No user metadata.
/// </summary>
public static class CompilerComments
{
    public const int LayoutV1MaxAsciiBytes = 128;

    public const string JumpCompanyDeny = "mfc:s:jump:company-deny";

    public const string JumpSiteDeny = "mfc:s:jump:site-deny";

    public const string JumpNodeDeny = "mfc:s:jump:node-deny";

    public const string ReturnCompanyDeny = "mfc:s:return:company-deny";

    public const string ReturnSiteDeny = "mfc:s:return:site-deny";

    public const string ReturnNodeDeny = "mfc:s:return:node-deny";

    public const string Terminal = "mfc:s:terminal";

    /// <summary>Logical-rule comment <c>mfc:r:&lt;uuid D&gt;:&lt;variant-index&gt;</c>.</summary>
    public static string LogicalRule(Guid ruleId, int variantIndex)
        => EnsureFits(string.Create(CultureInfo.InvariantCulture, $"mfc:r:{ruleId:D}:{variantIndex}"));

    /// <summary>Exception variant <c>…:ex</c> (Compiler Spec §23.1).</summary>
    public static string Exception(Guid ruleId, int variantIndex)
        => EnsureFits(LogicalRule(ruleId, variantIndex) + ":ex");

    /// <summary>FastTrack connection half <c>…:ft</c> (Compiler Spec §23.1).</summary>
    public static string FastTrack(Guid ruleId, int variantIndex)
        => EnsureFits(LogicalRule(ruleId, variantIndex) + ":ft");

    /// <summary>FastTrack accept half <c>…:ac</c> (Compiler Spec §23.1).</summary>
    public static string FastTrackAccept(Guid ruleId, int variantIndex)
        => EnsureFits(LogicalRule(ruleId, variantIndex) + ":ac");

    /// <summary>Rejects comments that exceed layout v1 ASCII size or contain non-ASCII bytes.</summary>
    public static string EnsureFits(string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);
        if (comment.Length > LayoutV1MaxAsciiBytes)
        {
            throw new DomainInvariantException(
                $"Generated comment must be at most {LayoutV1MaxAsciiBytes} ASCII bytes.");
        }

        foreach (char c in comment)
        {
            if (c > 0x7F)
            {
                throw new DomainInvariantException("Generated comment must be ASCII.");
            }
        }

        return comment;
    }
}
