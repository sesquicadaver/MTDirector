using System.Globalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Strict <c>mfc:guard:v1:</c> marker grammar (Onboarding Spec §15).
/// Marker must begin at the first character of the filter comment.
/// </summary>
public static class GuardMarker
{
    public const string Prefix = "mfc:guard:v1:";

    /// <summary>Formats <c>mfc:guard:v1:&lt;id&gt;:{4|6}:{i|o}:&lt;ordinal&gt;</c>.</summary>
    public static string Format(
        GuardProfileId profileId,
        IpAddressFamily family,
        FilterBuiltInContext chain,
        int ordinal)
    {
        if (ordinal < 0)
        {
            throw new DomainInvariantException("Guard marker ordinal must be non-negative.");
        }

        char familyCode = family switch
        {
            IpAddressFamily.IPv4 => '4',
            IpAddressFamily.IPv6 => '6',
            _ => throw new DomainInvariantException($"Unsupported guard family '{family}'."),
        };
        char direction = chain switch
        {
            FilterBuiltInContext.Input => 'i',
            FilterBuiltInContext.Output => 'o',
            _ => throw new DomainInvariantException(
                "Guard markers are only defined for INPUT and OUTPUT (Onboarding Spec §15)."),
        };

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}{profileId.Value}:{familyCode}:{direction}:{ordinal}");
    }

    /// <summary>
    /// Parses a strict guard marker that occupies the first token of <paramref name="comment"/>.
    /// </summary>
    public static bool TryParse(
        string? comment,
        out GuardProfileId profileId,
        out IpAddressFamily family,
        out FilterBuiltInContext chain,
        out int ordinal)
    {
        profileId = default;
        family = default;
        chain = default;
        ordinal = 0;
        if (!ActualFilterMarker.TryParseStrictGuardMarker(
                comment,
                out string profileIdHex,
                out char familyCode,
                out char directionCode,
                out ordinal))
        {
            return false;
        }

        try
        {
            profileId = GuardProfileId.Parse(profileIdHex);
        }
        catch (DomainInvariantException)
        {
            return false;
        }

        family = familyCode switch
        {
            '4' => IpAddressFamily.IPv4,
            '6' => IpAddressFamily.IPv6,
            _ => default,
        };
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            return false;
        }

        FilterBuiltInContext? parsedChain = directionCode switch
        {
            'i' => FilterBuiltInContext.Input,
            'o' => FilterBuiltInContext.Output,
            _ => null,
        };
        if (parsedChain is null)
        {
            return false;
        }

        chain = parsedChain.Value;
        return true;
    }

    /// <summary>True when the comment begins with a strict Spec §15 guard marker.</summary>
    public static bool IsStrictGuardComment(string? comment)
        => TryParse(comment, out _, out _, out _, out _);
}
