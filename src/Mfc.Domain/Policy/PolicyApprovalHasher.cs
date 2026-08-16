using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Analysis-bundle, dependency-fingerprint, and warning hashes (Policy Model §34.4 / §64).
/// Does not change the M2-12…M2-16 analysis-context combiners.
/// </summary>
public static class PolicyApprovalHasher
{
    /// <summary>
    /// SHA-256 of the Node analysis bundle (Policy Model §34.4) with a domain prefix
    /// matching other IncrementalHash contracts.
    /// </summary>
    public static Hash256 HashAnalysisBundle(
        Hash256 logicalEffectiveHash,
        IReadOnlyList<Hash256> perDeviceAnalysisHashes,
        Hash256 topologyProjectionHash,
        Hash256 impactSetHash)
    {
        ArgumentNullException.ThrowIfNull(logicalEffectiveHash);
        ArgumentNullException.ThrowIfNull(perDeviceAnalysisHashes);
        ArgumentNullException.ThrowIfNull(topologyProjectionHash);
        ArgumentNullException.ThrowIfNull(impactSetHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, PolicyApprovalCodes.BundlePrefix);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(logicalEffectiveHash.Bytes);
        AppendUInt32Be(hasher, (uint)perDeviceAnalysisHashes.Count);
        foreach (Hash256 deviceHash in perDeviceAnalysisHashes)
        {
            ArgumentNullException.ThrowIfNull(deviceHash);
            hasher.AppendData(deviceHash.Bytes);
        }

        hasher.AppendData(topologyProjectionHash.Bytes);
        hasher.AppendData(impactSetHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// SHA-256 of the approval dependency fingerprint (Policy Model §64).
    /// VRRP role and active WAN are not slots in this preimage.
    /// </summary>
    public static Hash256 HashDependencyFingerprint(PolicyApprovalDependencyVector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentException.ThrowIfNullOrWhiteSpace(vector.AnalyzerVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(vector.PolicySchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(vector.PipelineVersion);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, PolicyApprovalCodes.DependencyPrefix);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(vector.CompanyBindingHash.Bytes);
        hasher.AppendData(vector.SiteBindingHash.Bytes);
        hasher.AppendData(vector.NodeBindingHash.Bytes);
        hasher.AppendData(vector.ActiveExceptionsHash.Bytes);
        hasher.AppendData(vector.ZoneBindingHash.Bytes);
        hasher.AppendData(vector.NodeMembershipHash.Bytes);
        hasher.AppendData(vector.RouterOsConfigurationHash.Bytes);
        hasher.AppendData(vector.CapabilityHash.Bytes);
        hasher.AppendData(vector.CompatibilityHash.Bytes);
        hasher.AppendData(vector.ManagementAccessProfileHash.Bytes);
        hasher.AppendData(vector.AnchorGuardContextHash.Bytes);
        AppendUtf8(hasher, vector.AnalyzerVersion.Trim());
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, vector.PolicySchemaVersion.Trim());
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, vector.PipelineVersion.Trim());
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>SHA-256 of a warning identity used for exact-hash acknowledgment.</summary>
    public static Hash256 HashWarning(string code, string target, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, PolicyApprovalCodes.WarningPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, code.Trim());
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, target.Trim());
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, message.Trim());
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendUInt32Be(IncrementalHash hasher, uint value)
    {
        Span<byte> slot = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(slot, value);
        hasher.AppendData(slot);
    }
}
