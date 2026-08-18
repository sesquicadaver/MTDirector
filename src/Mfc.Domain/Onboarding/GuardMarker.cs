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
        if (string.IsNullOrWhiteSpace(comment)
            || !comment.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!ActualFilterMarker.TryReadMarker(comment, out string? marker)
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

        try
        {
            profileId = GuardProfileId.Parse(parts[3]);
        }
        catch (DomainInvariantException)
        {
            return false;
        }

        IpAddressFamily? parsedFamily = parts[4] switch
        {
            "4" => IpAddressFamily.IPv4,
            "6" => IpAddressFamily.IPv6,
            _ => null,
        };
        if (parsedFamily is null)
        {
            return false;
        }

        family = parsedFamily.Value;

        FilterBuiltInContext? parsedChain = parts[5] switch
        {
            "i" => FilterBuiltInContext.Input,
            "o" => FilterBuiltInContext.Output,
            _ => null,
        };
        if (parsedChain is null)
        {
            return false;
        }

        chain = parsedChain.Value;

        return int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out ordinal)
               && ordinal >= 0;
    }

    /// <summary>True when the comment begins with a strict Spec §15 guard marker.</summary>
    public static bool IsStrictGuardComment(string? comment)
        => TryParse(comment, out _, out _, out _, out _);
}
