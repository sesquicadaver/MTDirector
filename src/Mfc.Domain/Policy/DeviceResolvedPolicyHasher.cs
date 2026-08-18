using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Device-resolved policy identity (Compiler Spec §4 / §33.4). Excludes VRRP role and active WAN.
/// </summary>
public static class DeviceResolvedPolicyHasher
{
    public const string Prefix = "mfc.policy.device_resolved.v1";

    /// <summary>
    /// Hashes logical effective policy plus per-device resolved zone membership.
    /// <paramref name="resolvedZones"/> keys are zone ids; values are sorted interface names.
    /// </summary>
    public static Hash256 Hash(
        Hash256 logicalEffectivePolicyHash,
        DeviceId deviceId,
        IReadOnlyDictionary<ZoneId, IReadOnlyList<string>> resolvedZones,
        Hash256 capabilityHash)
    {
        ArgumentNullException.ThrowIfNull(logicalEffectivePolicyHash);
        ArgumentNullException.ThrowIfNull(resolvedZones);
        ArgumentNullException.ThrowIfNull(capabilityHash);

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, Prefix);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(logicalEffectivePolicyHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, deviceId.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        hasher.AppendData(capabilityHash.Bytes);
        hasher.AppendData([(byte)1]);
        foreach ((ZoneId zoneId, IReadOnlyList<string> members) in resolvedZones
                     .OrderBy(static kv => kv.Key.ToString(), StringComparer.Ordinal))
        {
            AppendUtf8(hasher, zoneId.ToString());
            hasher.AppendData([(byte)0]);
            foreach (string name in members.OrderBy(static n => n, StringComparer.Ordinal))
            {
                AppendUtf8(hasher, name);
                hasher.AppendData([(byte)0]);
            }

            hasher.AppendData([(byte)2]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>Extracts resolved zone membership from a zone compile context (no active WAN / VRRP role).</summary>
    /// <remarks>
    /// Fail-closed: every Node binding must resolve without blockers and without analysis stale
    /// (Compiler Spec §4 — zone bindings fully resolved before compile).
    /// </remarks>
    public static bool TryCaptureResolvedZones(
        ZoneServiceCompileContext zones,
        out IReadOnlyDictionary<ZoneId, IReadOnlyList<string>> captured,
        out string? errorCode,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(zones.Bindings);
        ArgumentNullException.ThrowIfNull(zones.Observation);
        captured = new Dictionary<ZoneId, IReadOnlyList<string>>();
        errorCode = null;
        errorMessage = null;
        Dictionary<ZoneId, IReadOnlyList<string>> map = [];
        foreach ((ZoneId zoneId, NodeZoneBinding binding) in zones.Bindings
                     .OrderBy(static kv => kv.Key.ToString(), StringComparer.Ordinal))
        {
            ZoneBindingResolveResult resolved = ZoneResolveEngine.Resolve(binding, zones.Observation);
            if (resolved.Blockers.Count > 0)
            {
                MapZoneBlocker(resolved.Blockers, out errorCode, out errorMessage);
                captured = new Dictionary<ZoneId, IReadOnlyList<string>>();
                return false;
            }

            if (resolved.AnalysisStale)
            {
                errorCode = PolicyCompilerCodes.CompilerAnalysisStale;
                errorMessage = $"Zone binding '{zoneId}' dependency hash is stale for compile.";
                captured = new Dictionary<ZoneId, IReadOnlyList<string>>();
                return false;
            }

            map[zoneId] = resolved.ResolvedMembers
                .OrderBy(static n => n, StringComparer.Ordinal)
                .ToArray();
        }

        captured = map;
        return true;
    }

    /// <summary>Extracts resolved zone membership; throws when any binding is unresolved or stale.</summary>
    public static IReadOnlyDictionary<ZoneId, IReadOnlyList<string>> CaptureResolvedZones(
        ZoneServiceCompileContext zones)
    {
        if (TryCaptureResolvedZones(zones, out IReadOnlyDictionary<ZoneId, IReadOnlyList<string>> captured, out _, out string? message))
        {
            return captured;
        }

        throw new DomainInvariantException(message ?? "Zone bindings are not fully resolved for compile.");
    }

    private static void MapZoneBlocker(
        IReadOnlyList<ZoneResolveBlocker> blockers,
        out string errorCode,
        out string errorMessage)
    {
        if (blockers.Any(static b => b.Code == ZoneResolveBlockerCodes.DynamicInterface))
        {
            ZoneResolveBlocker blocker = blockers.First(static b => b.Code == ZoneResolveBlockerCodes.DynamicInterface);
            errorCode = PolicyCompilerCodes.ZoneDynamicInterface;
            errorMessage = blocker.Message;
            return;
        }

        if (blockers.Any(static b => b.Code == ZoneResolveBlockerCodes.MissingInterface))
        {
            ZoneResolveBlocker blocker = blockers.First(static b => b.Code == ZoneResolveBlockerCodes.MissingInterface);
            errorCode = PolicyCompilerCodes.ZoneInterfaceMissing;
            errorMessage = blocker.Message;
            return;
        }

        if (blockers.Any(static b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet))
        {
            errorCode = PolicyCompilerCodes.ZoneEmpty;
            errorMessage = blockers.First(static b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet).Message;
            return;
        }

        ZoneResolveBlocker first = blockers[0];
        errorCode = PolicyCompilerCodes.ZoneNotResolved;
        errorMessage = first.Message;
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
