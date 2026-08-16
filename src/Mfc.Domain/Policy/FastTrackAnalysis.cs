using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// FastTrack safety validation (Policy Model §52 / M2-15).
/// Does not compile the ACCEPT fallback pair. Does not enable hardware FastTrack offload.
/// PCC/balanced/mixed and unknown Mangle block FastTrack; they do not block ordinary filter (M2-14).
/// </summary>
public static class FastTrackAnalysis
{
    public const string AnalyzerVersion = "mfc.fasttrack.v1";

    public const string FastTrackContextPrefix = "mfc.policy.fasttrack_context.v1";

    public const string AnalysisContextPrefix = "mfc.policy.analysis_context.v1";

    private static readonly HashSet<ConnectionState> AllowedStates = new()
    {
        ConnectionState.Established,
        ConnectionState.Related,
    };

    /// <summary>
    /// Validates every FASTTRACK_ACCEPT rule against the allowlist and topology.
    /// Non-FastTrack rules are ignored. Disabled FastTrack rules are still checked.
    /// </summary>
    public static FastTrackAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> rules,
        FastTrackTopologyContext topology,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? serviceCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(topology);
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog = serviceCatalog
            ?? new Dictionary<ServiceObjectId, ServiceObject>();

        List<FastTrackFinding> findings = [];
        List<PolicyRule> fasttrack = rules
            .Where(static r => r.Effect.Kind == PolicyRuleEffect.FasttrackAccept)
            .OrderBy(static r => r.Family)
            .ThenBy(static r => r.Chain)
            .ThenBy(static r => r.Ordinal)
            .ToList();

        foreach (PolicyRule rule in fasttrack)
        {
            CheckRule(rule, catalog, findings);
            CheckTopology(rule, topology, findings);
            if (rule.Logging.Enabled)
            {
                findings.Add(Finding(
                    FastTrackAnalysisCodes.LoggingUnsupported,
                    $"FASTTRACK_ACCEPT rule {rule.Id} enables logging; Compiler §21 forbids it.",
                    rule.Id.ToString()));
            }

            findings.Add(Finding(
                FastTrackAnalysisCodes.FallbackRequired,
                $"FASTTRACK_ACCEPT rule {rule.Id} requires an adjacent ACCEPT fallback pair.",
                rule.Id.ToString(),
                FastTrackAnalysisCodes.SeverityWarning));
        }

        if (topology.HasPreAnchorUnmanagedFastTrack && fasttrack.Count > 0)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses,
                "Unmanaged pre-anchor FastTrack bypasses managed FASTTRACK_ACCEPT policy.",
                "pre-anchor"));
        }

        IReadOnlyList<FastTrackFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Subject, f.Message))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Subject ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();

        return new FastTrackAnalysisResult
        {
            Findings = ordered,
            FastTrackContextHash = HashFastTrackContext(rules, topology),
            RequiresAcceptFallback = fasttrack.Count > 0,
            RiskFloor = fasttrack.Count > 0 ? FastTrackAnalysisCodes.RiskHigh : null,
        };
    }

    /// <summary>SHA-256 of FastTrack rule identity plus topology flags (enters analysis context).</summary>
    public static Hash256 HashFastTrackContext(
        IReadOnlyList<PolicyRule> rules,
        FastTrackTopologyContext topology)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(topology);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, FastTrackContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)(int)topology.UplinkMode]);
        hasher.AppendData([(byte)(topology.HasPcc ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasRoutingMarks ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasNonMainRoutingTables ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasUnknownMangle ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasVrf ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasPreAnchorUnmanagedFastTrack ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.ConnectionTrackingPresent ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasHotSpot ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasGlobalQueueTree ? 1 : 0)]);
        hasher.AppendData([(byte)(topology.HasPacketMarksRequiredAfterFastTrack ? 1 : 0)]);
        hasher.AppendData([(byte)1]);
        foreach (PolicyRule rule in rules
                     .Where(static r => r.Effect.Kind == PolicyRuleEffect.FasttrackAccept)
                     .OrderBy(static r => r.Id.ToString(), StringComparer.Ordinal))
        {
            AppendUtf8(hasher, rule.Id.ToString());
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(int)rule.Family]);
            hasher.AppendData([(byte)(int)rule.Chain]);
            hasher.AppendData([(byte)(int)rule.Stage]);
            hasher.AppendData([(byte)(rule.Enabled ? 1 : 0)]);
            hasher.AppendData([(byte)(rule.Logging.Enabled ? 1 : 0)]);
            AppendUtf8(hasher, rule.Logging.Prefix ?? string.Empty);
            hasher.AppendData([(byte)0]);
            if (rule.Predicate.ConnectionStates is { Count: > 0 } states)
            {
                foreach (ConnectionState state in states.OrderBy(static s => s))
                {
                    hasher.AppendData([(byte)(int)state]);
                }
            }

            hasher.AppendData([(byte)2]);
            if (rule.Predicate.Services is not null)
            {
                foreach (ServiceObjectId id in rule.Predicate.Services.Include
                             .OrderBy(static s => s.ToString(), StringComparer.Ordinal))
                {
                    AppendUtf8(hasher, id.ToString());
                    hasher.AppendData([(byte)0]);
                }
            }

            hasher.AppendData([(byte)(rule.Predicate.IpsecPolicy is null ? 0 : 1)]);
            hasher.AppendData([(byte)1]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// analysis_context_hash that includes M2-12…M2-14 slots plus this FastTrack slot.
    /// Does not change the one-, two-, three-, or four-argument combiners.
    /// </summary>
    public static Hash256 HashAnalysisContext(
        Hash256 actualFilterContextHash,
        Hash256 packetPathContextHash,
        Hash256 managementPathContextHash,
        Hash256 topologyDependencyContextHash,
        Hash256 fastTrackContextHash)
    {
        ArgumentNullException.ThrowIfNull(actualFilterContextHash);
        ArgumentNullException.ThrowIfNull(packetPathContextHash);
        ArgumentNullException.ThrowIfNull(managementPathContextHash);
        ArgumentNullException.ThrowIfNull(topologyDependencyContextHash);
        ArgumentNullException.ThrowIfNull(fastTrackContextHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalysisContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ActualFilterAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(actualFilterContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, PacketPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(packetPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ManagementPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(managementPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, TopologyDependencyAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(topologyDependencyContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(fastTrackContextHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// True when an unmanaged IPv4 FORWARD FastTrack sits before the managed anchor
    /// (or the builtin chain has no anchor). Does not re-walk the full filter CFG.
    /// </summary>
    public static bool HasPreAnchorUnmanagedFastTrack(IReadOnlyList<ActualFilterRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        List<ActualFilterRule> forward = rules
            .Where(static r =>
                r.Family == IpAddressFamily.IPv4
                && string.Equals(r.Chain, "forward", StringComparison.OrdinalIgnoreCase)
                && !ActualFilterMarker.IsManagedChainName(r.Chain))
            .OrderBy(static r => r.Ordinal)
            .ToList();
        if (forward.Count == 0)
        {
            return false;
        }

        int? anchor = null;
        foreach (ActualFilterRule rule in forward)
        {
            if (ActualFilterMarker.IsAnchor(rule.Comment))
            {
                anchor = rule.Ordinal;
                break;
            }
        }

        return forward.Any(r =>
            !r.Disabled
            && ActualFilterMarker.IsUnmanaged(r.Comment)
            && string.Equals(r.Action, "fasttrack-connection", StringComparison.OrdinalIgnoreCase)
            && (anchor is null || r.Ordinal < anchor.Value));
    }

    private static void CheckRule(
        PolicyRule rule,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog,
        List<FastTrackFinding> findings)
    {
        string subject = rule.Id.ToString();
        if (rule.Family != IpAddressFamily.IPv4)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                $"FASTTRACK_ACCEPT is forbidden on {rule.Family}; only IPv4 FORWARD is allowed.",
                subject));
        }

        if (rule.Chain != PolicyFilterChain.Forward)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                $"FASTTRACK_ACCEPT is forbidden on {rule.Chain}; only FORWARD is allowed.",
                subject));
        }

        if (rule.Stage != PolicyPipelineStage.StatePrelude)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                $"FASTTRACK_ACCEPT is forbidden on {PolicyPipelineV1.FormatStage(rule.Stage)}; only company STATE_PRELUDE is allowed.",
                subject));
        }

        if (!ConnectionStatesAllowed(rule.Predicate.ConnectionStates))
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT connection-state must be a subset of ESTABLISHED,RELATED.",
                subject));
        }

        if (!ProtocolsAllowed(rule, catalog, out string protocolMessage))
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                protocolMessage,
                subject));
        }

        if (rule.Predicate.IpsecPolicy is not null)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT cannot depend on IPsec; FastTrack bypasses IPsec assignment.",
                subject));
        }
    }

    private static void CheckTopology(
        PolicyRule rule,
        FastTrackTopologyContext topology,
        List<FastTrackFinding> findings)
    {
        string subject = rule.Id.ToString();
        if (topology.HasPcc)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT is forbidden when PCC is present.",
                subject));
        }

        if (topology.UplinkMode is DeclaredUplinkMode.Balanced or DeclaredUplinkMode.Mixed
            || topology.UplinkMode == DeclaredUplinkMode.None)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                $"FASTTRACK_ACCEPT is forbidden on uplink mode {topology.UplinkMode}.",
                subject));
        }

        if (topology.HasRoutingMarks)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT is forbidden when routing marks are present.",
                subject));
        }

        if (topology.HasNonMainRoutingTables)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT is forbidden when non-main routing tables are present.",
                subject));
        }

        if (topology.HasUnknownMangle)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT is forbidden when Mangle dependencies are indeterminate.",
                subject));
        }

        if (topology.HasVrf)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT is forbidden when a VRF dependency is present.",
                subject));
        }

        if (topology.HasPacketMarksRequiredAfterFastTrack)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.ContextUnsupported,
                "FASTTRACK_ACCEPT is forbidden when packet marks are required after the FastTrack point.",
                subject));
        }

        if (!topology.ConnectionTrackingPresent || topology.HasHotSpot || topology.HasGlobalQueueTree)
        {
            findings.Add(Finding(
                FastTrackAnalysisCodes.CapabilityUnsupported,
                "FASTTRACK_ACCEPT requires proven connection tracking without HotSpot or global queue-tree.",
                subject));
        }
    }

    private static bool ConnectionStatesAllowed(IReadOnlyList<ConnectionState>? states)
    {
        if (states is null || states.Count == 0)
        {
            return false;
        }

        return states.All(AllowedStates.Contains);
    }

    private static bool ProtocolsAllowed(
        PolicyRule rule,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog,
        out string message)
    {
        ServiceSelector? services = rule.Predicate.Services;
        if (services is null || services.MatchesAnyProtocol)
        {
            message = "FASTTRACK_ACCEPT protocol must be a TCP or UDP subset; unconstrained protocol is forbidden.";
            return false;
        }

        foreach (ServiceObjectId id in services.Include)
        {
            if (!catalog.TryGetValue(id, out ServiceObject? obj))
            {
                message = $"FASTTRACK_ACCEPT service '{id}' is missing from the catalog; TCP/UDP subset cannot be proven.";
                return false;
            }

            foreach (ServiceTerm term in obj.Terms)
            {
                if (term.Protocol.IsAny
                    || (term.Protocol.Number != IpProtocol.Tcp && term.Protocol.Number != IpProtocol.Udp))
                {
                    message = "FASTTRACK_ACCEPT protocol must be a subset of TCP or UDP.";
                    return false;
                }
            }
        }

        message = string.Empty;
        return true;
    }

    private static FastTrackFinding Finding(
        string code,
        string message,
        string? subject,
        string? severity = null)
        => new()
        {
            Code = code,
            Severity = severity ?? FastTrackAnalysisCodes.SeverityBlocker,
            Message = message,
            Subject = subject,
            Risk = FastTrackAnalysisCodes.RiskHigh,
        };

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
