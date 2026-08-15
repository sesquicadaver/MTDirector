using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Bounded actual RouterOS filter CFG and pre/post-anchor analysis (Policy Model §44–§45 / M2-12).
/// Does not move unmanaged rules. RouterOS implicit accept is never the managed default.
/// </summary>
public static class ActualFilterAnalysis
{
    public const string AnalyzerVersion = "mfc.actual-filter.v1";

    public const string ActualContextPrefix = "mfc.policy.actual_filter_context.v1";

    public const string AnalysisContextPrefix = "mfc.policy.analysis_context.v1";

    private static readonly HashSet<string> BuiltinChains = new(StringComparer.Ordinal)
    {
        "input",
        "forward",
        "output",
    };

    private static readonly HashSet<string> SupportedActions = new(StringComparer.Ordinal)
    {
        "accept",
        "drop",
        "reject",
        "fasttrack-connection",
        "jump",
        "return",
        "log",
        "passthrough",
    };

    private static readonly HashSet<string> UnsupportedActions = new(StringComparer.Ordinal)
    {
        "add-src-to-address-list",
        "add-dst-to-address-list",
        "tarpit",
    };

    /// <summary>
    /// Analyzes actual filter rules against candidate chain contracts.
    /// Caller supplies rules in live RouterOS order; this type does not reorder across captures.
    /// </summary>
    public static ActualFilterAnalysisResult Analyze(
        IReadOnlyList<ActualFilterRule> rules,
        ChainContractSet contracts)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(contracts);

        List<ActualFilterFinding> findings = [];
        GraphBuilder graph = new();
        Dictionary<ChainKey, List<ActualFilterRule>> byChain = Index(rules);
        if (byChain.Count > ActualFilterAnalysisCodes.MaxChains)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.AnalysisIndeterminate,
                "Actual filter exceeds the 1024-chain CFG limit."));
        }

        HashSet<ChainKey> postAnchorWalked = [];
        foreach (ChainKey builtin in BuiltinSurfaces(byChain))
        {
            WalkBuiltin(builtin, byChain, contracts, graph, findings, postAnchorWalked);
        }

        IReadOnlyList<ActualFilterFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Family, f.Chain, f.Ordinal, f.Message))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Family)
            .ThenBy(static f => f.Chain, StringComparer.Ordinal)
            .ThenBy(static f => f.Ordinal)
            .ToArray();
        Hash256 actualHash = HashActualContext(rules);
        return new ActualFilterAnalysisResult
        {
            Findings = ordered,
            Graph = graph.Freeze(),
            ActualContextHash = actualHash,
            AnalysisContextHash = HashAnalysisContext(actualHash),
            PostAnchorAnalyzed = postAnchorWalked.Count > 0,
            UsesRouterOsImplicitAcceptAsManagedDefault = false,
        };
    }

    /// <summary>SHA-256 of ordered actual-filter identity (enters analysis context).</summary>
    public static Hash256 HashActualContext(IReadOnlyList<ActualFilterRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, ActualContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        foreach (ActualFilterRule rule in rules
                     .OrderBy(static r => r.Family)
                     .ThenBy(static r => r.Chain, StringComparer.Ordinal)
                     .ThenBy(static r => r.Ordinal))
        {
            AppendUtf8(hasher, FormatFamily(rule.Family));
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Chain);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Ordinal.ToString(CultureInfo.InvariantCulture));
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Dynamic ? "1" : "0");
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Disabled ? "1" : "0");
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Action ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.JumpTarget ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Comment ?? string.Empty);
            hasher.AppendData([(byte)0]);
            foreach (KeyValuePair<string, string> pair in rule.KnownMatchers
                         .Concat(rule.UnknownMatchers)
                         .OrderBy(static kv => kv.Key, StringComparer.Ordinal)
                         .ThenBy(static kv => kv.Value, StringComparer.Ordinal))
            {
                AppendUtf8(hasher, pair.Key);
                hasher.AppendData([(byte)0]);
                AppendUtf8(hasher, pair.Value);
                hasher.AppendData([(byte)0]);
            }

            hasher.AppendData([(byte)1]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// analysis_context_hash slot that currently holds the actual filter context hash
    /// (Policy Model §34.3). Management-path / topology slots belong to later issues.
    /// </summary>
    public static Hash256 HashAnalysisContext(Hash256 actualFilterContextHash)
    {
        ArgumentNullException.ThrowIfNull(actualFilterContextHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalysisContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(actualFilterContextHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void WalkBuiltin(
        ChainKey builtin,
        Dictionary<ChainKey, List<ActualFilterRule>> byChain,
        ChainContractSet contracts,
        GraphBuilder graph,
        List<ActualFilterFinding> findings,
        HashSet<ChainKey> postAnchorWalked)
    {
        List<ActualFilterRule> chainRules = byChain.TryGetValue(builtin, out List<ActualFilterRule>? listed)
            ? listed
            : [];
        List<ActualFilterRule> anchors = chainRules.Where(static r => ActualFilterMarker.IsAnchor(r.Comment)).ToList();
        if (anchors.Count > 1)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.AnalysisIndeterminate,
                $"Family {FormatFamily(builtin.Family)} chain '{builtin.Chain}' has {anchors.Count} anchors.",
                builtin.Family,
                builtin.Chain,
                anchors[0].Ordinal));
            return;
        }

        int? anchorOrdinal = anchors.Count == 1 ? anchors[0].Ordinal : null;
        if (anchors.Count == 1 && anchors[0].Disabled)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.PreAnchorIndeterminate,
                $"Anchor on {FormatFamily(builtin.Family)}/{builtin.Chain} is disabled.",
                builtin.Family,
                builtin.Chain,
                anchors[0].Ordinal));
        }

        bool returnToUnmanaged = ReturnsToUnmanaged(contracts, builtin);
        HashSet<string> jumpStack = [];
        _ = Walk(
            builtin,
            0,
            depth: 0,
            jumpStack,
            byChain,
            graph,
            findings,
            anchorOrdinal,
            returnToUnmanaged,
            postAnchorWalked,
            callerKey: null,
            inheritedRegion: Region.PreAnchor,
            returnToId: null);
    }

    private static WalkOutcome Walk(
        ChainKey key,
        int index,
        int depth,
        HashSet<string> jumpStack,
        Dictionary<ChainKey, List<ActualFilterRule>> byChain,
        GraphBuilder graph,
        List<ActualFilterFinding> findings,
        int? anchorOrdinal,
        bool returnToUnmanaged,
        HashSet<ChainKey> postAnchorWalked,
        ChainKey? callerKey,
        Region inheritedRegion,
        string? returnToId)
    {
        List<ActualFilterRule> chainRules = byChain.TryGetValue(key, out List<ActualFilterRule>? listed)
            ? listed
            : [];
        bool builtin = BuiltinChains.Contains(key.Chain);
        string? previousId = null;
        while (true)
        {
            if (graph.IsOverflowed)
            {
                EnsureLimitFinding(
                    findings,
                    ActualFilterAnalysisCodes.AnalysisIndeterminate,
                    "Actual filter exceeded the 50000-node CFG limit.");
                return WalkOutcome.Stopped;
            }

            if (index >= chainRules.Count)
            {
                if (builtin)
                {
                    string fallId = graph.AddSynthetic(
                        ActualFilterGraphNodeKind.RouterOsImplicitAccept,
                        key,
                        action: "accept");
                    if (previousId is not null)
                    {
                        graph.AddEdge(previousId, fallId, ActualFilterGraphEdgeKind.Fallthrough);
                    }

                    if (callerKey is not null && inheritedRegion == Region.PreAnchor)
                    {
                        findings.Add(Finding(
                            ActualFilterAnalysisCodes.PreAnchorAcceptBypasses,
                            $"Unmanaged jump fallthrough reaches RouterOS implicit accept on {FormatFamily(key.Family)}/{key.Chain}.",
                            key.Family,
                            key.Chain));
                    }

                    return WalkOutcome.Stopped;
                }

                string retId = graph.AddSynthetic(ActualFilterGraphNodeKind.Return, key, action: "return");
                if (previousId is not null)
                {
                    graph.AddEdge(previousId, retId, ActualFilterGraphEdgeKind.Fallthrough);
                }

                if (returnToId is not null)
                {
                    graph.AddEdge(retId, returnToId, ActualFilterGraphEdgeKind.Return);
                }

                return WalkOutcome.Continues;
            }

            ActualFilterRule rule = chainRules[index];
            string nodeId = graph.AddRule(rule);
            if (previousId is not null)
            {
                graph.AddEdge(previousId, nodeId, ActualFilterGraphEdgeKind.Fallthrough);
            }

            previousId = nodeId;
            if (graph.IsOverflowed)
            {
                EnsureLimitFinding(
                    findings,
                    ActualFilterAnalysisCodes.AnalysisIndeterminate,
                    "Actual filter exceeded the 50000-node CFG limit.");
                return WalkOutcome.Stopped;
            }

            if (rule.Disabled)
            {
                index++;
                continue;
            }

            Region region = ClassifyRegion(builtin, rule.Ordinal, anchorOrdinal, inheritedRegion);
            if (region == Region.PostAnchor && returnToUnmanaged)
            {
                postAnchorWalked.Add(key);
            }

            CollectRuleFindings(rule, region, findings);
            string? action = rule.Action;
            if (action is null || !SupportedActions.Contains(action))
            {
                index++;
                continue;
            }

            if (ActualFilterMarker.IsAnchor(rule.Comment))
            {
                string managedId = graph.AddSynthetic(
                    ActualFilterGraphNodeKind.ManagedPipeline,
                    key,
                    ordinal: rule.Ordinal,
                    action: "jump");
                graph.AddEdge(nodeId, managedId, ActualFilterGraphEdgeKind.Jump);
                if (!returnToUnmanaged)
                {
                    return WalkOutcome.Stopped;
                }

                postAnchorWalked.Add(key);
                previousId = managedId;
                index++;
                continue;
            }

            switch (action)
            {
                case "accept":
                case "drop":
                case "reject":
                case "fasttrack-connection":
                case "log":
                case "passthrough":
                    index++;
                    continue;
                case "return":
                    string returnId = graph.AddSynthetic(
                        ActualFilterGraphNodeKind.Return,
                        key,
                        rule.Ordinal,
                        "return");
                    graph.AddEdge(nodeId, returnId, ActualFilterGraphEdgeKind.Return);
                    if (returnToId is not null)
                    {
                        graph.AddEdge(returnId, returnToId, ActualFilterGraphEdgeKind.Return);
                    }

                    index++;
                    continue;
                case "jump":
                    string? successorId = index + 1 < chainRules.Count
                        ? NodeId(key, chainRules[index + 1].Ordinal)
                        : null;
                    _ = WalkJump(
                        key,
                        depth,
                        jumpStack,
                        byChain,
                        graph,
                        findings,
                        returnToUnmanaged,
                        postAnchorWalked,
                        rule,
                        nodeId,
                        region,
                        successorId);
                    if (graph.IsOverflowed)
                    {
                        EnsureLimitFinding(
                            findings,
                            ActualFilterAnalysisCodes.AnalysisIndeterminate,
                            "Actual filter exceeded the 50000-node CFG limit.");
                        return WalkOutcome.Stopped;
                    }

                    index++;
                    continue;
                default:
                    index++;
                    continue;
            }
        }
    }

    private static WalkOutcome WalkJump(
        ChainKey key,
        int depth,
        HashSet<string> jumpStack,
        Dictionary<ChainKey, List<ActualFilterRule>> byChain,
        GraphBuilder graph,
        List<ActualFilterFinding> findings,
        bool returnToUnmanaged,
        HashSet<ChainKey> postAnchorWalked,
        ActualFilterRule rule,
        string nodeId,
        Region region,
        string? successorId)
    {
        if (string.IsNullOrWhiteSpace(rule.JumpTarget))
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.AnalysisIndeterminate,
                $"Jump on {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal} has no target.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
            MaybePreAnchorIndeterminate(rule, region, findings);
            return WalkOutcome.Stopped;
        }

        if (depth >= ActualFilterAnalysisCodes.MaxJumpDepth)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.DepthLimit,
                $"Jump depth exceeded {ActualFilterAnalysisCodes.MaxJumpDepth} at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
            MaybePreAnchorIndeterminate(rule, region, findings);
            return WalkOutcome.Stopped;
        }

        if (ActualFilterMarker.IsManagedChainName(rule.JumpTarget))
        {
            if (ActualFilterMarker.IsUnmanaged(rule.Comment))
            {
                findings.Add(Finding(
                    ActualFilterAnalysisCodes.AnalysisIndeterminate,
                    $"Unmanaged jump into managed chain '{rule.JumpTarget}' at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                    rule.Family,
                    rule.Chain,
                    rule.Ordinal));
                MaybePreAnchorIndeterminate(rule, region, findings);
                return WalkOutcome.Stopped;
            }

            string managedId = graph.AddSynthetic(
                ActualFilterGraphNodeKind.ManagedPipeline,
                key,
                rule.Ordinal,
                "jump");
            graph.AddEdge(nodeId, managedId, ActualFilterGraphEdgeKind.Jump);
            return WalkOutcome.Stopped;
        }

        ChainKey target = new(rule.Family, rule.JumpTarget);
        string sourceKey = StackKey(key);
        string targetKey = StackKey(target);
        if (jumpStack.Contains(targetKey) || targetKey == sourceKey)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.JumpCycle,
                $"Jump cycle involving '{rule.JumpTarget}' at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
            MaybePreAnchorIndeterminate(rule, region, findings);
            return WalkOutcome.Stopped;
        }

        if (!byChain.ContainsKey(target) && !BuiltinChains.Contains(target.Chain))
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.AnalysisIndeterminate,
                $"Jump target '{rule.JumpTarget}' is missing at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
            MaybePreAnchorIndeterminate(rule, region, findings);
            return WalkOutcome.Stopped;
        }

        graph.AddEdge(nodeId, TargetEntryId(target, byChain), ActualFilterGraphEdgeKind.Jump);
        jumpStack.Add(sourceKey);
        WalkOutcome nested = Walk(
            target,
            0,
            depth + 1,
            jumpStack,
            byChain,
            graph,
            findings,
            anchorOrdinal: AnchorOrdinal(target, byChain),
            returnToUnmanaged,
            postAnchorWalked,
            callerKey: key,
            inheritedRegion: region,
            returnToId: successorId);
        jumpStack.Remove(sourceKey);
        return nested;
    }

    private static void CollectRuleFindings(
        ActualFilterRule rule,
        Region region,
        List<ActualFilterFinding> findings)
    {
        bool unknownAction = rule.Action is null
                             || UnsupportedActions.Contains(rule.Action)
                             || !SupportedActions.Contains(rule.Action);
        if (unknownAction)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.UnknownAction,
                $"Unsupported filter action '{rule.Action ?? "(null)"}' on {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
        }

        if (rule.UnknownMatchers.Count > 0)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.UnknownMatcher,
                $"Unknown matcher on {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
        }

        if (region != Region.PreAnchor || !ActualFilterMarker.IsUnmanaged(rule.Comment))
        {
            if (unknownAction || rule.UnknownMatchers.Count > 0)
            {
                findings.Add(Finding(
                    ActualFilterAnalysisCodes.AnalysisIndeterminate,
                    $"Actual-filter analysis is indeterminate at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                    rule.Family,
                    rule.Chain,
                    rule.Ordinal));
            }

            return;
        }

        if (rule.Dynamic)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.PreAnchorDynamicRule,
                $"Dynamic pre-anchor rule on {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
        }

        if (unknownAction || rule.UnknownMatchers.Count > 0)
        {
            findings.Add(Finding(
                ActualFilterAnalysisCodes.PreAnchorIndeterminate,
                $"Pre-anchor analysis is indeterminate at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                rule.Family,
                rule.Chain,
                rule.Ordinal));
            return;
        }

        switch (rule.Action)
        {
            case "accept":
                findings.Add(Finding(
                    ActualFilterAnalysisCodes.PreAnchorAcceptBypasses,
                    $"Pre-anchor ACCEPT bypasses managed policy at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                    rule.Family,
                    rule.Chain,
                    rule.Ordinal));
                break;
            case "drop":
            case "reject":
                findings.Add(Finding(
                    ActualFilterAnalysisCodes.PreAnchorDropShadows,
                    $"Pre-anchor {rule.Action!.ToUpperInvariant()} shadows managed policy at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                    rule.Family,
                    rule.Chain,
                    rule.Ordinal));
                break;
            case "fasttrack-connection":
                findings.Add(Finding(
                    ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses,
                    $"Pre-anchor FastTrack bypasses managed policy at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
                    rule.Family,
                    rule.Chain,
                    rule.Ordinal));
                break;
        }
    }

    private static void MaybePreAnchorIndeterminate(
        ActualFilterRule rule,
        Region region,
        List<ActualFilterFinding> findings)
    {
        if (region != Region.PreAnchor || !ActualFilterMarker.IsUnmanaged(rule.Comment))
        {
            return;
        }

        findings.Add(Finding(
            ActualFilterAnalysisCodes.PreAnchorIndeterminate,
            $"Pre-anchor analysis is indeterminate at {FormatFamily(rule.Family)}/{rule.Chain}#{rule.Ordinal}.",
            rule.Family,
            rule.Chain,
            rule.Ordinal));
    }

    private static Region ClassifyRegion(bool builtin, int ordinal, int? anchorOrdinal, Region inherited)
    {
        if (!builtin)
        {
            return inherited;
        }

        if (anchorOrdinal is null)
        {
            return Region.PreAnchor;
        }

        if (ordinal < anchorOrdinal.Value)
        {
            return Region.PreAnchor;
        }

        if (ordinal > anchorOrdinal.Value)
        {
            return Region.PostAnchor;
        }

        return Region.Anchor;
    }

    private static int? AnchorOrdinal(ChainKey key, Dictionary<ChainKey, List<ActualFilterRule>> byChain)
    {
        if (!BuiltinChains.Contains(key.Chain) || !byChain.TryGetValue(key, out List<ActualFilterRule>? listed))
        {
            return null;
        }

        List<ActualFilterRule> anchors = listed.Where(static r => ActualFilterMarker.IsAnchor(r.Comment)).ToList();
        return anchors.Count == 1 ? anchors[0].Ordinal : null;
    }

    private static string TargetEntryId(
        ChainKey target,
        Dictionary<ChainKey, List<ActualFilterRule>> byChain)
    {
        if (byChain.TryGetValue(target, out List<ActualFilterRule>? list) && list.Count > 0)
        {
            return NodeId(target, list[0].Ordinal);
        }

        return BuiltinChains.Contains(target.Chain)
            ? $"fallthrough:{FormatFamily(target.Family)}:{target.Chain}"
            : $"return:{FormatFamily(target.Family)}:{target.Chain}";
    }

    private static bool ReturnsToUnmanaged(ChainContractSet contracts, ChainKey builtin)
    {
        if (!TryParseBuiltin(builtin.Chain, out PolicyFilterChain chain))
        {
            return false;
        }

        ChainContract? contract = contracts.Find(builtin.Family, chain);
        return contract is { DefaultDisposition: ChainDefaultDisposition.ReturnToUnmanaged };
    }

    private static bool TryParseBuiltin(string name, out PolicyFilterChain chain)
    {
        switch (name)
        {
            case "input":
                chain = PolicyFilterChain.Input;
                return true;
            case "forward":
                chain = PolicyFilterChain.Forward;
                return true;
            case "output":
                chain = PolicyFilterChain.Output;
                return true;
            default:
                chain = default;
                return false;
        }
    }

    private static Dictionary<ChainKey, List<ActualFilterRule>> Index(IReadOnlyList<ActualFilterRule> rules)
    {
        Dictionary<ChainKey, List<ActualFilterRule>> map = [];
        foreach (ActualFilterRule rule in rules)
        {
            ChainKey key = new(rule.Family, rule.Chain);
            if (!map.TryGetValue(key, out List<ActualFilterRule>? list))
            {
                list = [];
                map[key] = list;
            }

            list.Add(rule);
        }

        foreach (List<ActualFilterRule> list in map.Values)
        {
            list.Sort(static (a, b) => a.Ordinal.CompareTo(b.Ordinal));
        }

        return map;
    }

    private static IEnumerable<ChainKey> BuiltinSurfaces(Dictionary<ChainKey, List<ActualFilterRule>> byChain)
    {
        HashSet<ChainKey> keys = [];
        foreach (ChainKey key in byChain.Keys)
        {
            if (BuiltinChains.Contains(key.Chain))
            {
                keys.Add(key);
            }
            else
            {
                keys.Add(new ChainKey(key.Family, "forward"));
            }
        }

        if (keys.Count == 0)
        {
            keys.Add(new ChainKey(IpAddressFamily.IPv4, "forward"));
        }

        return keys.OrderBy(static k => k.Family).ThenBy(static k => k.Chain, StringComparer.Ordinal);
    }

    private static ActualFilterFinding Finding(
        string code,
        string message,
        IpAddressFamily? family = null,
        string? chain = null,
        int? ordinal = null)
        => new()
        {
            Code = code,
            Severity = ActualFilterAnalysisCodes.SeverityBlocker,
            Message = message,
            Family = family,
            Chain = chain,
            Ordinal = ordinal,
        };

    private static void EnsureLimitFinding(List<ActualFilterFinding> findings, string code, string message)
    {
        if (findings.Exists(f => f.Code == code && f.Message == message))
        {
            return;
        }

        findings.Add(Finding(code, message));
    }

    private static string NodeId(ChainKey key, int ordinal)
        => $"rule:{FormatFamily(key.Family)}:{key.Chain}:{ordinal.ToString(CultureInfo.InvariantCulture)}";

    private static string StackKey(ChainKey key)
        => $"{FormatFamily(key.Family)}:{key.Chain}";

    private static string FormatFamily(IpAddressFamily family)
        => family == IpAddressFamily.IPv6 ? "ipv6" : "ipv4";

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private enum WalkOutcome : byte
    {
        Continues = 0,
        Stopped = 1,
    }

    private enum Region : byte
    {
        PreAnchor = 0,
        Anchor = 1,
        PostAnchor = 2,
    }

    private readonly record struct ChainKey(IpAddressFamily Family, string Chain);

    private sealed class GraphBuilder
    {
        private readonly Dictionary<string, ActualFilterGraphNode> _nodes = new(StringComparer.Ordinal);
        private readonly List<ActualFilterGraphEdge> _edges = [];

        public bool IsOverflowed => _nodes.Count > ActualFilterAnalysisCodes.MaxGraphNodes;

        public string AddRule(ActualFilterRule rule)
        {
            string id = NodeId(new ChainKey(rule.Family, rule.Chain), rule.Ordinal);
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            _nodes[id] = new ActualFilterGraphNode
            {
                Id = id,
                Kind = ActualFilterGraphNodeKind.Rule,
                Family = rule.Family,
                Chain = rule.Chain,
                Ordinal = rule.Ordinal,
                Action = rule.Action,
            };
            return id;
        }

        public string AddSynthetic(
            ActualFilterGraphNodeKind kind,
            ChainKey key,
            int? ordinal = null,
            string? action = null)
        {
            string prefix = kind switch
            {
                ActualFilterGraphNodeKind.RouterOsImplicitAccept => "fallthrough",
                ActualFilterGraphNodeKind.Return => "return",
                ActualFilterGraphNodeKind.ManagedPipeline => "managed",
                _ => "node",
            };
            string id = ordinal is null
                ? $"{prefix}:{FormatFamily(key.Family)}:{key.Chain}"
                : $"{prefix}:{FormatFamily(key.Family)}:{key.Chain}:{ordinal.Value.ToString(CultureInfo.InvariantCulture)}";
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            _nodes[id] = new ActualFilterGraphNode
            {
                Id = id,
                Kind = kind,
                Family = key.Family,
                Chain = key.Chain,
                Ordinal = ordinal,
                Action = action,
            };
            return id;
        }

        public void AddEdge(string fromId, string toId, ActualFilterGraphEdgeKind kind)
        {
            if (_edges.Exists(e => e.FromId == fromId && e.ToId == toId && e.Kind == kind))
            {
                return;
            }

            _edges.Add(new ActualFilterGraphEdge
            {
                FromId = fromId,
                ToId = toId,
                Kind = kind,
            });
        }

        public ActualFilterGraph Freeze()
            => new()
            {
                Nodes = _nodes.Values
                    .OrderBy(static n => n.Id, StringComparer.Ordinal)
                    .ToArray(),
                Edges = _edges
                    .OrderBy(static e => e.FromId, StringComparer.Ordinal)
                    .ThenBy(static e => e.ToId, StringComparer.Ordinal)
                    .ToArray(),
            };
    }
}
