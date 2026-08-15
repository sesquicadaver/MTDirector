using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Emits analysis BLOCKERs from packet-path class for managed FORWARD (next-1 / N1-04).
/// Does not disable L2/L3 hardware offload. Does not mutate policy or RouterOS.
/// </summary>
public static class PacketPathAnalysis
{
    public const string AnalyzerVersion = "mfc.packet-path.v1";

    public const string PacketPathContextPrefix = "mfc.policy.packet_path_context.v1";

    public const string AnalysisContextPrefix = "mfc.policy.analysis_context.v1";

    /// <summary>
    /// Maps next-1 path class onto managed-FORWARD blockers:
    /// HARDWARE_OFFLOADED_PATH → <see cref="PacketPathAnalysisCodes.BypassesIpFirewall"/>,
    /// INDETERMINATE → <see cref="PacketPathAnalysisCodes.NotProven"/>.
    /// CPU and MIXED do not emit those codes.
    /// </summary>
    public static PacketPathAnalysisResult Analyze(IReadOnlyList<PacketPathPairFact> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        List<PacketPathFinding> findings = [];
        foreach (PacketPathPairFact pair in pairs)
        {
            ArgumentNullException.ThrowIfNull(pair);
            switch (pair.PathClass)
            {
                case PacketPathKind.HardwareOffloadedPath:
                    findings.Add(Finding(
                        PacketPathAnalysisCodes.BypassesIpFirewall,
                        $"Pair {pair.IngressInterface}→{pair.EgressInterface} classified HARDWARE_OFFLOADED_PATH (managed FORWARD not proven on CPU).",
                        pair));
                    break;
                case PacketPathKind.Indeterminate:
                    findings.Add(Finding(
                        PacketPathAnalysisCodes.NotProven,
                        $"Pair {pair.IngressInterface}→{pair.EgressInterface} classified INDETERMINATE (packet path through IP firewall not proven).",
                        pair));
                    break;
                case PacketPathKind.CpuFirewallPath:
                case PacketPathKind.MixedPath:
                    break;
                default:
                    findings.Add(Finding(
                        PacketPathAnalysisCodes.NotProven,
                        $"Pair {pair.IngressInterface}→{pair.EgressInterface} has an unknown packet-path class.",
                        pair));
                    break;
            }
        }

        PacketPathFinding[] ordered = findings
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.IngressInterface, StringComparer.Ordinal)
            .ThenBy(static f => f.EgressInterface, StringComparer.Ordinal)
            .ThenBy(static f => f.VlanId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        return new PacketPathAnalysisResult
        {
            Findings = ordered,
            PacketPathContextHash = HashPacketPathContext(pairs),
            BlocksManagedForwardPolicy = ordered.Length > 0,
        };
    }

    /// <summary>SHA-256 of ordered pair identity and class (enters analysis context).</summary>
    public static Hash256 HashPacketPathContext(IReadOnlyList<PacketPathPairFact> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, PacketPathContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        foreach (PacketPathPairFact pair in pairs
                     .OrderBy(static p => p.Bridge ?? string.Empty, StringComparer.Ordinal)
                     .ThenBy(static p => p.IngressInterface, StringComparer.Ordinal)
                     .ThenBy(static p => p.EgressInterface, StringComparer.Ordinal)
                     .ThenBy(static p => p.VlanId ?? string.Empty, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, pair.Bridge ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, pair.IngressInterface);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, pair.EgressInterface);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, pair.VlanId ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, FormatClass(pair.PathClass));
            hasher.AppendData([(byte)1]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// analysis_context_hash that includes the M2-12 actual-filter slot plus this packet-path slot
    /// (Policy Model §34.3 relevant observation hash). Does not change the one-argument
    /// <see cref="ActualFilterAnalysis.HashAnalysisContext(Hash256)"/> preimage.
    /// </summary>
    public static Hash256 HashAnalysisContext(Hash256 actualFilterContextHash, Hash256 packetPathContextHash)
    {
        ArgumentNullException.ThrowIfNull(actualFilterContextHash);
        ArgumentNullException.ThrowIfNull(packetPathContextHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalysisContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ActualFilterAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(actualFilterContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(packetPathContextHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    public static PacketPathKind ParseClassName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().ToUpperInvariant() switch
        {
            "CPU_FIREWALL_PATH" or "CPUFIREWALLPATH" => PacketPathKind.CpuFirewallPath,
            "HARDWARE_OFFLOADED_PATH" or "HARDWAREOFFLOADEDPATH" => PacketPathKind.HardwareOffloadedPath,
            "MIXED_PATH" or "MIXEDPATH" => PacketPathKind.MixedPath,
            "INDETERMINATE" => PacketPathKind.Indeterminate,
            _ => throw new DomainInvariantException($"Unknown packet path class '{name}'."),
        };
    }

    private static PacketPathFinding Finding(string code, string message, PacketPathPairFact pair)
        => new()
        {
            Code = code,
            Severity = PacketPathAnalysisCodes.SeverityBlocker,
            Message = message,
            IngressInterface = pair.IngressInterface,
            EgressInterface = pair.EgressInterface,
            Bridge = pair.Bridge,
            VlanId = pair.VlanId,
        };

    private static string FormatClass(PacketPathKind kind)
        => kind switch
        {
            PacketPathKind.CpuFirewallPath => "CPU_FIREWALL_PATH",
            PacketPathKind.HardwareOffloadedPath => "HARDWARE_OFFLOADED_PATH",
            PacketPathKind.MixedPath => "MIXED_PATH",
            PacketPathKind.Indeterminate => "INDETERMINATE",
            _ => ((int)kind).ToString(CultureInfo.InvariantCulture),
        };

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
