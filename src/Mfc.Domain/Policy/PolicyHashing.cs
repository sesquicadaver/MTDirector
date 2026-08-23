using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Policy revision content, parent-context, and logical-effective hash contracts
/// (Policy Model §8, §33–§34). Content hash is always over exact uncompressed canonical bytes.
/// Logical effective hash MUST NOT be <c>HashContent(PolicyCanonicalWriter.Write(synthetic document))</c>.
/// </summary>
public static class PolicyHashing
{
    public const string ParentContextPrefix = "mfc.policy.parent_context.v1";

    /// <summary>UTF-8 prefix for <see cref="HashLogicalEffective"/> (NUL-terminated in the preimage).</summary>
    public const string LogicalEffectivePrefix = "mfc.policy.logical_effective.v1";

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
            PolicyKind.IncidentDenyOverlay => HashComposite(
                PolicyKind.IncidentDenyOverlay,
                Require(companyBaselineHash, "INCIDENT_DENY_OVERLAY requires company baseline hash."),
                siteOverlayHash,
                Require(nodeOverlayHash, "INCIDENT_DENY_OVERLAY requires node overlay hash.")),
            _ => throw new DomainInvariantException($"Unknown policy kind '{kind}'."),
        };
    }

    /// <summary>
    /// SHA-256 of the logical-effective preimage (Policy Model §34.1 / LOCK-10 IncrementalHash).
    /// Absent site/node overlays omit their 32-byte slots (no zero padding) unless
    /// <paramref name="padAbsentSiteWithZeros"/> is set for anti-B1 tests.
    /// Exception slot is uint32 BE count followed by N×32-byte exception content hashes.
    /// </summary>
    public static Hash256 HashLogicalEffective(
        uint schemaVersion,
        Hash256 companyContentHash,
        Hash256? siteContentHash,
        Hash256? nodeContentHash,
        IReadOnlyList<Hash256> exceptionContentHashes,
        IReadOnlyList<byte[]> canonicalMergedObjects,
        IReadOnlyList<byte[]> canonicalActiveRules,
        byte[] chainContractBytes,
        bool padAbsentSiteWithZeros = false,
        bool includeExceptionCountSlot = true)
    {
        byte[] preimage = BuildLogicalEffectivePreimage(
            schemaVersion,
            companyContentHash,
            siteContentHash,
            nodeContentHash,
            exceptionContentHashes,
            canonicalMergedObjects,
            canonicalActiveRules,
            chainContractBytes,
            padAbsentSiteWithZeros,
            includeExceptionCountSlot);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(preimage);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// Exact logical-effective preimage bytes. Exposed so tests can prove prefix+NUL and
    /// the uint32 exception-count slot (D19) without hashing a synthetic <see cref="PolicyDocument"/>.
    /// </summary>
    public static byte[] BuildLogicalEffectivePreimage(
        uint schemaVersion,
        Hash256 companyContentHash,
        Hash256? siteContentHash,
        Hash256? nodeContentHash,
        IReadOnlyList<Hash256> exceptionContentHashes,
        IReadOnlyList<byte[]> canonicalMergedObjects,
        IReadOnlyList<byte[]> canonicalActiveRules,
        byte[] chainContractBytes,
        bool padAbsentSiteWithZeros = false,
        bool includeExceptionCountSlot = true)
    {
        ArgumentNullException.ThrowIfNull(companyContentHash);
        ArgumentNullException.ThrowIfNull(exceptionContentHashes);
        ArgumentNullException.ThrowIfNull(canonicalMergedObjects);
        ArgumentNullException.ThrowIfNull(canonicalActiveRules);
        ArgumentNullException.ThrowIfNull(chainContractBytes);

        using MemoryStream stream = new();
        WriteUtf8(stream, LogicalEffectivePrefix);
        stream.WriteByte(0);
        WriteUtf8(stream, PolicyDocument.SchemaName);
        stream.WriteByte(0);
        WriteUInt32Be(stream, schemaVersion);
        WriteUtf8(stream, PolicyPipelineV1.Version);
        stream.WriteByte(0);
        stream.Write(companyContentHash.Bytes);
        if (siteContentHash is not null)
        {
            stream.Write(siteContentHash.Bytes);
        }
        else if (padAbsentSiteWithZeros)
        {
            stream.Write(new byte[Hash256.Size]);
        }

        if (nodeContentHash is not null)
        {
            stream.Write(nodeContentHash.Bytes);
        }

        if (includeExceptionCountSlot)
        {
            WriteUInt32Be(stream, (uint)exceptionContentHashes.Count);
            foreach (Hash256 digest in exceptionContentHashes)
            {
                ArgumentNullException.ThrowIfNull(digest);
                stream.Write(digest.Bytes);
            }
        }

        foreach (byte[] objectBytes in canonicalMergedObjects)
        {
            ArgumentNullException.ThrowIfNull(objectBytes);
            stream.Write(objectBytes);
        }

        foreach (byte[] ruleBytes in canonicalActiveRules)
        {
            ArgumentNullException.ThrowIfNull(ruleBytes);
            stream.Write(ruleBytes);
        }

        stream.Write(chainContractBytes);
        return stream.ToArray();
    }

    private static void WriteUtf8(Stream stream, string value)
        => stream.Write(Encoding.UTF8.GetBytes(value));

    private static void WriteUInt32Be(Stream stream, uint value)
    {
        Span<byte> slot = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(slot, value);
        stream.Write(slot);
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
