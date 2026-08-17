using System.Globalization;
using Mfc.Domain.Inventory;

namespace Mfc.Domain.Policy;

/// <summary>
/// Deterministic managed-chain and address-list naming (Compiler Spec §8, M3-02).
/// Namespaces are <c>mfc4</c> / <c>mfc6</c>; physical RouterOS anchors are never created here.
/// </summary>
public static class ManagedChainNamespace
{
    public const string LayoutVersion = "1";

    public const string Ipv4Prefix = "mfc4";

    public const string Ipv6Prefix = "mfc6";

    /// <summary>Family namespace token: <c>mfc4</c> or <c>mfc6</c>.</summary>
    public static string FamilyPrefix(IpAddressFamily family)
        => family switch
        {
            IpAddressFamily.IPv4 => Ipv4Prefix,
            IpAddressFamily.IPv6 => Ipv6Prefix,
            _ => throw new DomainInvariantException($"Unsupported IP family for managed chain namespace: {family}."),
        };

    /// <summary>Family digit used in anchor markers: <c>4</c> or <c>6</c>.</summary>
    public static string FamilyDigit(IpAddressFamily family)
        => family switch
        {
            IpAddressFamily.IPv4 => "4",
            IpAddressFamily.IPv6 => "6",
            _ => throw new DomainInvariantException($"Unsupported IP family for managed chain namespace: {family}."),
        };

    /// <summary>Built-in chain code: <c>i</c>, <c>f</c>, or <c>o</c>.</summary>
    public static string BuiltInCode(FilterBuiltInContext builtIn)
        => builtIn switch
        {
            FilterBuiltInContext.Input => "i",
            FilterBuiltInContext.Forward => "f",
            FilterBuiltInContext.Output => "o",
            _ => throw new DomainInvariantException($"Unsupported built-in context for managed chain namespace: {builtIn}."),
        };

    /// <summary>Role code: <c>r</c>, <c>dc</c>, <c>ds</c>, or <c>dn</c>.</summary>
    public static string RoleCode(FilterChainArtifactRole role)
        => role switch
        {
            FilterChainArtifactRole.Root => "r",
            FilterChainArtifactRole.CompanyDeny => "dc",
            FilterChainArtifactRole.SiteDeny => "ds",
            FilterChainArtifactRole.NodeDeny => "dn",
            _ => throw new DomainInvariantException($"Unsupported chain role for managed chain namespace: {role}."),
        };

    /// <summary>
    /// Managed filter chain name: <c>mfc{4|6}.{i|f|o}.{r|dc|ds|dn}.&lt;artifact-id&gt;</c>.
    /// </summary>
    public static string ChainName(
        IpAddressFamily family,
        FilterBuiltInContext builtIn,
        FilterChainArtifactRole role,
        string artifactId)
    {
        string id = NormalizeArtifactId(artifactId);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FamilyPrefix(family)}.{BuiltInCode(builtIn)}.{RoleCode(role)}.{id}");
    }

    /// <summary>
    /// Managed address-list name: <c>mfc{4|6}.a.&lt;list-id&gt;</c> (Compiler Spec §8.4).
    /// </summary>
    public static string AddressListName(IpAddressFamily family, string listId)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{FamilyPrefix(family)}.a.{NormalizeListId(listId)}");

    /// <summary>Desired permanent-anchor marker comment (Compiler Spec §9); physical anchor is not created.</summary>
    public static string DesiredAnchorComment(IpAddressFamily family, FilterBuiltInContext builtIn)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"mfc:anchor:v1:{FamilyDigit(family)}:{BuiltInCode(builtIn)}");

    /// <summary>Validates the 16-hex <c>artifact_id</c> token used in managed resource names.</summary>
    public static string NormalizeArtifactId(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new DomainInvariantException("Managed chain artifact id is required.");
        }

        string normalized = artifactId.Trim().ToLowerInvariant();
        if (normalized.Length != RouterOsFilterArtifactIdentity.ArtifactIdHexLength)
        {
            throw new DomainInvariantException(
                $"Managed chain artifact id must be {RouterOsFilterArtifactIdentity.ArtifactIdHexLength} hex characters.");
        }

        foreach (char c in normalized)
        {
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            {
                continue;
            }

            throw new DomainInvariantException("Managed chain artifact id must be lowercase hex.");
        }

        return normalized;
    }

    private static string NormalizeListId(string listId)
    {
        if (string.IsNullOrWhiteSpace(listId))
        {
            throw new DomainInvariantException("Managed address-list id is required.");
        }

        string normalized = listId.Trim().ToLowerInvariant();
        if (normalized.Length != RouterOsFilterArtifactIdentity.ArtifactIdHexLength)
        {
            throw new DomainInvariantException(
                $"Managed address-list id must be {RouterOsFilterArtifactIdentity.ArtifactIdHexLength} hex characters.");
        }

        foreach (char c in normalized)
        {
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            {
                continue;
            }

            throw new DomainInvariantException("Managed address-list id must be lowercase hex.");
        }

        return normalized;
    }
}
