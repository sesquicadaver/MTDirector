using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Policy revision content and parent-context hash contracts (Policy Model §8, §33–§34).
/// Content hash is always over exact uncompressed canonical bytes.
/// </summary>
public static class PolicyHashing
{
    public const string ParentContextPrefix = "mfc.policy.parent_context.v1";

    /// <summary>policy_revision_hash = SHA256(exact canonical revision bytes).</summary>
    public static Hash256 HashContent(ReadOnlySpan<byte> canonicalBytes)
    {
        if (canonicalBytes.IsEmpty)
        {
            throw new DomainInvariantException("Policy revision canonical bytes must be non-empty.");
        }

        return Hash256.Create(SHA256.HashData(canonicalBytes));
    }

    public static Hash256 HashContent(PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return HashContent(PolicyCanonicalWriter.Write(document));
    }

    /// <summary>
    /// Builds <c>parent_context_hash</c> for the given kind.
    /// COMPANY_BASELINE has no parent context (returns null).
    /// SITE_OVERLAY stores the company baseline content hash directly (Policy Model §8).
    /// NODE_OVERLAY / EXCEPTION use a prefixed composite over ordered parent digests.
    /// </summary>
    public static Hash256? ComputeParentContextHash(
        PolicyKind kind,
        Hash256? companyBaselineHash,
        Hash256? siteOverlayHash,
        Hash256? nodeOverlayHash,
        Hash256? waivedRuleHash)
    {
        return kind switch
        {
            PolicyKind.CompanyBaseline => null,
            PolicyKind.SiteOverlay => Require(companyBaselineHash, "SITE_OVERLAY requires company baseline hash."),
            PolicyKind.NodeOverlay => HashComposite(
                PolicyKind.NodeOverlay,
                Require(companyBaselineHash, "NODE_OVERLAY requires company baseline hash."),
                siteOverlayHash),
            PolicyKind.Exception => HashComposite(
                PolicyKind.Exception,
                Require(companyBaselineHash, "EXCEPTION requires company baseline hash."),
                siteOverlayHash,
                nodeOverlayHash,
                Require(waivedRuleHash, "EXCEPTION requires waived rule hash.")),
            _ => throw new DomainInvariantException($"Unknown policy kind '{kind}'."),
        };
    }

    private static Hash256 HashComposite(PolicyKind kind, params Hash256?[] ordered)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, ParentContextPrefix);
        AppendNull(hasher);
        AppendUtf8(hasher, PolicyCanonicalWriter.FormatKind(kind));
        AppendNull(hasher);
        foreach (Hash256? component in ordered)
        {
            if (component is null)
            {
                continue;
            }

            hasher.AppendData(component.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static Hash256 Require(Hash256? hash, string message)
        => hash ?? throw new DomainInvariantException(message);

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendNull(IncrementalHash hasher)
        => hasher.AppendData([(byte)0]);
}
