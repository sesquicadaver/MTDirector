using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Compilation provenance (Compiler Spec §5). <c>compiled_at</c> is never part of artifact hashes.</summary>
public sealed class CompilationProvenance
{
    public required DeviceId DeviceId { get; init; }

    public required Hash256 LogicalEffectivePolicyHash { get; init; }

    public required Hash256 DeviceResolvedPolicyHash { get; init; }

    public required Hash256 AnalysisBundleHash { get; init; }

    public required Hash256 CapabilityHash { get; init; }

    public required Hash256 CompilerProfileHash { get; init; }

    public required string CompilerVersion { get; init; }

    public required DateTimeOffset CompiledAtUtc { get; init; }
}

/// <summary>
/// Semantic compile summary for API responses (Issue Set M3-07 AC#9).
/// Contains no RouterOS command strings.
/// </summary>
public sealed class FilterArtifactSemanticSummary
{
    public required DeviceId DeviceId { get; init; }

    public required string ArtifactId { get; init; }

    public required Hash256 ResourceHash { get; init; }

    public required Hash256 PhysicalSemanticsHash { get; init; }

    public required Hash256 LogicalEffectivePolicyHash { get; init; }

    public required Hash256 DeviceResolvedPolicyHash { get; init; }

    public required Hash256 AnalysisBundleHash { get; init; }

    public required int AddressListCount { get; init; }

    public required int ChainCount { get; init; }

    public required int RuleCount { get; init; }

    public required int AnchorTargetCount { get; init; }

    public static FilterArtifactSemanticSummary From(
        RouterOsFilterArtifact artifact,
        CompilationProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(provenance);
        return new FilterArtifactSemanticSummary
        {
            DeviceId = artifact.DeviceId,
            ArtifactId = artifact.ArtifactId,
            ResourceHash = artifact.ResourceHash,
            PhysicalSemanticsHash = artifact.PhysicalSemanticsHash,
            LogicalEffectivePolicyHash = provenance.LogicalEffectivePolicyHash,
            DeviceResolvedPolicyHash = provenance.DeviceResolvedPolicyHash,
            AnalysisBundleHash = provenance.AnalysisBundleHash,
            AddressListCount = artifact.AddressLists.Length,
            ChainCount = artifact.Chains.Length,
            RuleCount = artifact.Chains.Sum(static c => c.Rules.Length),
            AnchorTargetCount = artifact.AnchorTargets.Length,
        };
    }
}

/// <summary>Per-device compiler input after analysis/approval gates (Compiler Spec §4).</summary>
public sealed class DeviceFilterCompileRequest
{
    public const string CompilerVersion = "mfc.compiler.v1";

    public required DeviceId DeviceId { get; init; }

    public required Hash256 LogicalEffectivePolicyHash { get; init; }

    public required Hash256 AnalysisBundleHash { get; init; }

    public required Hash256 CapabilityHash { get; init; }

    public required Hash256 CompilerProfileHash { get; init; }

    /// <summary>True when the bound analysis run has no BLOCKERs and required tests PASS.</summary>
    public required bool AnalysisPassed { get; init; }

    /// <summary>True when desired binding / revision is APPROVED for this analysis bundle.</summary>
    public required bool InputApproved { get; init; }

    /// <summary>True when analysis dependency fingerprint still matches current inventory/policy deps.</summary>
    public required bool AnalysisContextCurrent { get; init; }

    /// <summary>True when capability_hash matches the current device capability profile.</summary>
    public required bool CapabilityCurrent { get; init; }

    /// <summary>True when compiler_profile_hash is supported by this Controller build.</summary>
    public required bool CompilerProfileSupported { get; init; }

    /// <summary>Inventory Node kind; Switch FORWARD is fail-closed (Compiler Spec §32).</summary>
    public required NodeKind NodeKind { get; init; }

    public required IReadOnlyList<PolicyRule> ActiveRules { get; init; }

    public required ChainContractSet ChainContracts { get; init; }

    public required IReadOnlyDictionary<AddressObjectId, AddressObject> Addresses { get; init; }

    public required IReadOnlyDictionary<ServiceObjectId, ServiceObject> Services { get; init; }

    public required ZoneServiceCompileContext Zones { get; init; }

    public FastTrackTopologyContext? FastTrackTopology { get; init; }

    public required DateTimeOffset CompiledAtUtc { get; init; }
}

/// <summary>One device compile outcome.</summary>
public sealed class DeviceFilterCompileResult
{
    private DeviceFilterCompileResult(
        bool isSuccess,
        string? code,
        string? message,
        RouterOsFilterArtifact? artifact,
        CompilationProvenance? provenance,
        FilterArtifactSemanticSummary? summary)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Artifact = artifact;
        Provenance = provenance;
        Summary = summary;
    }

    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    public RouterOsFilterArtifact? Artifact { get; }

    public CompilationProvenance? Provenance { get; }

    public FilterArtifactSemanticSummary? Summary { get; }

    public static DeviceFilterCompileResult Ok(
        RouterOsFilterArtifact artifact,
        CompilationProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(provenance);
        return new DeviceFilterCompileResult(
            true,
            null,
            null,
            artifact,
            provenance,
            FilterArtifactSemanticSummary.From(artifact, provenance));
    }

    public static DeviceFilterCompileResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new DeviceFilterCompileResult(false, code, message, null, null, null);
    }
}

/// <summary>Node-wide compile outcome: all Devices or fail-closed (Issue Set M3-07 AC#8).</summary>
public sealed class NodeFilterCompileResult
{
    private NodeFilterCompileResult(
        bool isSuccess,
        string? code,
        string? message,
        Hash256? logicalEffectivePolicyHash,
        IReadOnlyList<DeviceFilterCompileResult> devices)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        LogicalEffectivePolicyHash = logicalEffectivePolicyHash;
        Devices = devices;
    }

    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    public Hash256? LogicalEffectivePolicyHash { get; }

    public IReadOnlyList<DeviceFilterCompileResult> Devices { get; }

    public IReadOnlyList<FilterArtifactSemanticSummary> Summaries
        => Devices
            .Where(static d => d.IsSuccess && d.Summary is not null)
            .Select(static d => d.Summary!)
            .ToArray();

    public static NodeFilterCompileResult Ok(
        Hash256 logicalEffectivePolicyHash,
        IReadOnlyList<DeviceFilterCompileResult> devices)
    {
        ArgumentNullException.ThrowIfNull(logicalEffectivePolicyHash);
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0 || devices.Any(static d => !d.IsSuccess))
        {
            throw new DomainInvariantException("Successful node compile requires every device result to succeed.");
        }

        return new NodeFilterCompileResult(true, null, null, logicalEffectivePolicyHash, devices);
    }

    public static NodeFilterCompileResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new NodeFilterCompileResult(false, code, message, null, []);
    }
}

/// <summary>
/// Per-device filter compile orchestration (M3-07). Pure Domain: no RouterOS writes, no VRRP role / active WAN inputs.
/// </summary>
public sealed class DeviceFilterCompiler
{
    private readonly FilterMatcherEffectCompiler _matcherCompiler;

    public DeviceFilterCompiler(FilterRuleCompileLimits? limits = null)
        => _matcherCompiler = new FilterMatcherEffectCompiler(limits);

    /// <summary>Compiles one Device after gate checks. Active WAN on <see cref="ZoneServiceCompileContext"/> is ignored.</summary>
    public DeviceFilterCompileResult Compile(DeviceFilterCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DeviceFilterCompileResult? gate = TryGate(request);
        if (gate is not null)
        {
            return gate;
        }

        ArgumentNullException.ThrowIfNull(request.ActiveRules);
        ArgumentNullException.ThrowIfNull(request.ChainContracts);
        ArgumentNullException.ThrowIfNull(request.Addresses);
        ArgumentNullException.ThrowIfNull(request.Services);
        ArgumentNullException.ThrowIfNull(request.Zones);

        if (request.ChainContracts.Count == 0)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Chain contracts are required before filter compilation.");
        }

        if (request.NodeKind == NodeKind.Switch
            && (request.ChainContracts.Items.Any(static c => c.Chain == PolicyFilterChain.Forward)
                || request.ActiveRules.Any(static r => r.Chain == PolicyFilterChain.Forward)))
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.SwitchForwardCompilationForbidden,
                "Switch nodes forbid FORWARD filter compilation.");
        }

        ZoneServiceCompileContext zones = new()
        {
            DeviceId = request.Zones.DeviceId,
            Bindings = request.Zones.Bindings,
            Observation = request.Zones.Observation,
            Services = request.Services,
            ActiveWanName = null,
        };

        if (!DeviceResolvedPolicyHasher.TryCaptureResolvedZones(
                zones,
                out IReadOnlyDictionary<ZoneId, IReadOnlyList<string>> resolvedZones,
                out string? zoneCode,
                out string? zoneMessage))
        {
            return DeviceFilterCompileResult.Fail(zoneCode!, zoneMessage!);
        }

        Hash256 deviceResolved = DeviceResolvedPolicyHasher.Hash(
            request.LogicalEffectivePolicyHash,
            request.DeviceId,
            resolvedZones,
            request.CapabilityHash);

        FilterMatcherCompileContext matcherContext = new()
        {
            Zones = zones,
            Addresses = request.Addresses,
            FastTrackTopology = request.FastTrackTopology,
        };

        FilterRuleCompileResult physical = _matcherCompiler.Compile(request.ActiveRules, matcherContext);
        if (!physical.IsSuccess)
        {
            return DeviceFilterCompileResult.Fail(physical.Code!, physical.Message!);
        }

        Dictionary<Guid, PolicyRule> rulesById = request.ActiveRules.ToDictionary(static r => r.Id.Value);
        List<ManagedChainSurfacePlan> surfaces = [];
        foreach (ChainContract contract in request.ChainContracts.Items)
        {
            FilterBuiltInContext builtIn = MapBuiltIn(contract.Chain);
            surfaces.Add(new ManagedChainSurfacePlan
            {
                Family = contract.Family,
                BuiltInContext = builtIn,
                DefaultDisposition = contract.DefaultDisposition,
                RejectModeValue = contract.RejectModeValue,
                ProtectedControlPlane = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.ProtectedControlPlane),
                IncidentPreStateDeny = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.IncidentPreStateDeny),
                MandatoryPreStateDeny = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.MandatoryPreStateDeny),
                StatePrelude = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.StatePrelude),
                CompanyDenyBody = CollectDenyBody(
                    physical.Rules,
                    rulesById,
                    contract,
                    PolicyPipelineStage.CompanyDenyExemptions,
                    PolicyPipelineStage.CompanyDeny),
                SiteDenyBody = CollectDenyBody(
                    physical.Rules,
                    rulesById,
                    contract,
                    PolicyPipelineStage.SiteDenyExemptions,
                    PolicyPipelineStage.SiteDeny),
                NodeDenyBody = CollectDenyBody(
                    physical.Rules,
                    rulesById,
                    contract,
                    PolicyPipelineStage.NodeDenyExemptions,
                    PolicyPipelineStage.NodeDeny),
                CompanyAllow = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.CompanyAllow),
                SiteAllow = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.SiteAllow),
                NodeAllow = Collect(physical.Rules, rulesById, contract, PolicyPipelineStage.NodeAllow),
            });
        }

        Hash256 physicalSemantics = RouterOsFilterArtifactIdentity.HashPhysicalSemantics(
            BuildSemantics(request, physical.Rules, resolvedZones));
        RouterOsFilterArtifact artifact = ManagedChainLayoutBuilder.Build(new ManagedChainLayoutRequest
        {
            CompilerProfileHash = request.CompilerProfileHash,
            PhysicalSemanticsHash = physicalSemantics,
            DeviceId = request.DeviceId,
            Surfaces = surfaces,
            AddressLists = physical.InternedLists,
            EmitDesiredAnchorTargets = true,
        });

        if (artifact.CanonicalBytes.Length > FilterArtifactLimits.LayoutV1MaxCanonicalBytes)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.ArtifactSizeLimit,
                $"Encoded filter artifact exceeds {FilterArtifactLimits.LayoutV1MaxCanonicalBytes} bytes.");
        }

        CompilationProvenance provenance = new()
        {
            DeviceId = request.DeviceId,
            LogicalEffectivePolicyHash = request.LogicalEffectivePolicyHash,
            DeviceResolvedPolicyHash = deviceResolved,
            AnalysisBundleHash = request.AnalysisBundleHash,
            CapabilityHash = request.CapabilityHash,
            CompilerProfileHash = request.CompilerProfileHash,
            CompilerVersion = DeviceFilterCompileRequest.CompilerVersion,
            CompiledAtUtc = request.CompiledAtUtc,
        };
        return DeviceFilterCompileResult.Ok(artifact, provenance);
    }

    /// <summary>
    /// Compiles every Device for a Node with a shared logical effective hash.
    /// Any device failure yields an empty result (AC#8).
    /// </summary>
    public NodeFilterCompileResult CompileNode(IReadOnlyList<DeviceFilterCompileRequest> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        if (devices.Count == 0)
        {
            return NodeFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Node filter compile requires at least one Device.");
        }

        Hash256 logical = devices[0].LogicalEffectivePolicyHash;
        List<DeviceFilterCompileResult> results = [];
        foreach (DeviceFilterCompileRequest request in devices)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.LogicalEffectivePolicyHash.Equals(logical))
            {
                return NodeFilterCompileResult.Fail(
                    PolicyCompilerCodes.CompilerInputNotApproved,
                    "All Devices on a Node must share the same logical_effective_policy_hash.");
            }

            DeviceFilterCompileResult compiled = Compile(request);
            if (!compiled.IsSuccess)
            {
                return NodeFilterCompileResult.Fail(compiled.Code!, compiled.Message!);
            }

            results.Add(compiled);
        }

        return NodeFilterCompileResult.Ok(logical, results);
    }

    private static DeviceFilterCompileResult? TryGate(DeviceFilterCompileRequest request)
    {
        if (!request.InputApproved)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Compiler requires an approved analysis-bound policy input.");
        }

        if (!request.AnalysisPassed)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Compiler requires analysis result PASS.");
        }

        if (!request.AnalysisContextCurrent)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerAnalysisStale,
                "Compiler requires a current analysis context.");
        }

        if (!request.CapabilityCurrent)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerCapabilityStale,
                "Compiler requires a current capability profile.");
        }

        if (!request.CompilerProfileSupported)
        {
            return DeviceFilterCompileResult.Fail(
                PolicyCompilerCodes.CompilerProfileUnsupported,
                "Compiler profile is not supported by this Controller.");
        }

        return null;
    }

    private static List<FilterRuleArtifact> Collect(
        IReadOnlyList<FilterRuleArtifact> physical,
        IReadOnlyDictionary<Guid, PolicyRule> rulesById,
        ChainContract contract,
        PolicyPipelineStage stage)
    {
        List<FilterRuleArtifact> list = [];
        foreach (FilterRuleArtifact artifact in physical)
        {
            if (artifact.LogicalRuleId is not Guid id || !rulesById.TryGetValue(id, out PolicyRule? rule))
            {
                continue;
            }

            if (rule.Family == contract.Family && rule.Chain == contract.Chain && rule.Stage == stage)
            {
                list.Add(artifact);
            }
        }

        return list;
    }

    private static List<FilterRuleArtifact> CollectDenyBody(
        IReadOnlyList<FilterRuleArtifact> physical,
        IReadOnlyDictionary<Guid, PolicyRule> rulesById,
        ChainContract contract,
        PolicyPipelineStage exemptions,
        PolicyPipelineStage deny)
    {
        List<FilterRuleArtifact> body = [];
        body.AddRange(Collect(physical, rulesById, contract, exemptions));
        body.AddRange(Collect(physical, rulesById, contract, deny));
        return body;
    }

    private static PhysicalSemanticsMaterial BuildSemantics(
        DeviceFilterCompileRequest request,
        IReadOnlyList<FilterRuleArtifact> physical,
        IReadOnlyDictionary<ZoneId, IReadOnlyList<string>> resolvedZones)
    {
        List<Guid> ruleIds = request.ActiveRules
            .Select(static r => r.Id.Value)
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
        List<string> predicates = physical
            .Select(static r => DigestMatchers(r))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToList();
        List<string> zones = resolvedZones
            .Select(static kv =>
                kv.Key + ":" + string.Join(',', kv.Value.OrderBy(static n => n, StringComparer.Ordinal)))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToList();
        List<string> actions = physical
            .Select(static r => r.Action + ":" + string.Join(',', r.ActionParameters.Select(static p => p.Key + "=" + p.Value)))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToList();
        List<string> logging = physical
            .Select(static r => r.Log ? "on:" + (r.LogPrefix ?? string.Empty) : "off")
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToList();
        List<string> contracts = request.ChainContracts.Items
            .Select(static c =>
                PolicyPipelineV1.FormatFamily(c.Family) + "/" +
                PolicyPipelineV1.FormatFilterChain(c.Chain) + "/" +
                PolicyPipelineV1.FormatDisposition(c.DefaultDisposition) + "/" +
                (c.RejectModeValue is null ? "-" : PolicyPipelineV1.FormatRejectMode(c.RejectModeValue.Value)))
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToList();

        return new PhysicalSemanticsMaterial
        {
            LayoutVersion = ManagedChainNamespace.LayoutVersion,
            CompilerProfileHash = request.CompilerProfileHash,
            RuleIds = ruleIds,
            ResolvedPredicateDigests = predicates,
            ResolvedZoneDigests = zones,
            ActionDigests = actions,
            LoggingDigests = logging,
            ChainContractDigests = contracts,
        };
    }

    private static string DigestMatchers(FilterRuleArtifact rule)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string key, string value) in rule.Matchers)
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(key));
            hasher.AppendData([(byte)0]);
            hasher.AppendData(Encoding.UTF8.GetBytes(value));
            hasher.AppendData([(byte)0]);
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    private static FilterBuiltInContext MapBuiltIn(PolicyFilterChain chain)
        => chain switch
        {
            PolicyFilterChain.Input => FilterBuiltInContext.Input,
            PolicyFilterChain.Forward => FilterBuiltInContext.Forward,
            PolicyFilterChain.Output => FilterBuiltInContext.Output,
            _ => throw new DomainInvariantException($"Unsupported filter chain '{chain}'."),
        };
}
