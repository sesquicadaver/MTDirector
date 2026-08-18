using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

/// <summary>Command to compile and persist filter artifacts for every Device on a Node (M3-07).</summary>
public sealed class CompileNodeFilterArtifactsCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required byte[] CurrentDependencyFingerprint { get; init; }

    /// <summary>
    /// Current device capability hash from the dependency vector (Compiler Spec §4).
    /// Compared to each Device's latest completed snapshot capability.
    /// </summary>
    public required byte[] CurrentCapabilityHash { get; init; }

    /// <summary>When null, Controller uses <see cref="RouterOsCompilerProfile.LayoutV1Hash"/>.</summary>
    public byte[]? CompilerProfileHash { get; init; }
}

/// <summary>Semantic compile response (no RouterOS commands).</summary>
public sealed class CompileNodeFilterArtifactsView
{
    public required Guid NodeId { get; init; }

    public required byte[] LogicalEffectivePolicyHash { get; init; }

    public required string LogicalEffectivePolicyHashHex { get; init; }

    public required IReadOnlyList<FilterArtifactSummaryView> Artifacts { get; init; }
}

/// <summary>One Device artifact summary for API responses (Issue Set M3-07 AC#9).</summary>
public sealed class FilterArtifactSummaryView
{
    public required Guid DeviceId { get; init; }

    public required string ArtifactId { get; init; }

    public required byte[] ResourceHash { get; init; }

    public required string ResourceHashHex { get; init; }

    public required byte[] PhysicalSemanticsHash { get; init; }

    public required byte[] DeviceResolvedPolicyHash { get; init; }

    public required byte[] AnalysisBundleHash { get; init; }

    public required int AddressListCount { get; init; }

    public required int ChainCount { get; init; }

    public required int RuleCount { get; init; }

    public required int AnchorTargetCount { get; init; }

    public required bool StoredAsNew { get; init; }
}

/// <summary>
/// Compiles approved analysis-bound filter artifacts for all Node Devices and stores them
/// content-addressed (M3-07). Fail-closed: no partial Node success.
/// </summary>
public sealed class CompileNodeFilterArtifactsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly IPolicyStore _policies;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IZoneDefinitionStore _zones;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IZoneResolveObservationSource _observations;
    private readonly ISnapshotStore _snapshots;
    private readonly IFilterArtifactStore _artifacts;
    private readonly IClock _clock;
    private readonly DeviceFilterCompiler _compiler = new();

    public CompileNodeFilterArtifactsUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        IPolicyStore policies,
        IPolicyApprovalStore approvals,
        IZoneDefinitionStore zones,
        INodeZoneBindingStore bindings,
        IZoneResolveObservationSource observations,
        ISnapshotStore snapshots,
        IFilterArtifactStore artifacts,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _policies = policies;
        _approvals = approvals;
        _zones = zones;
        _bindings = bindings;
        _observations = observations;
        _snapshots = snapshots;
        _artifacts = artifacts;
        _clock = clock;
    }

    public async Task<ApplicationResult<CompileNodeFilterArtifactsView>> ExecuteAsync(
        CompileNodeFilterArtifactsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.PolicyWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? fingerprintError = PolicyRevisionSupport.TryHash(
            command.CurrentDependencyFingerprint,
            "current_dependency_fingerprint",
            out Hash256? currentFingerprint);
        if (fingerprintError is not null || currentFingerprint is null)
        {
            return ApplicationResults.Fail(fingerprintError!);
        }

        ApplicationError? capabilityError = PolicyRevisionSupport.TryHash(
            command.CurrentCapabilityHash,
            "current_capability_hash",
            out Hash256? currentCapability);
        if (capabilityError is not null || currentCapability is null)
        {
            return ApplicationResults.Fail(capabilityError!);
        }

        Hash256 compilerProfile = RouterOsCompilerProfile.LayoutV1Hash;
        if (command.CompilerProfileHash is not null)
        {
            ApplicationError? profileError = PolicyRevisionSupport.TryHash(
                command.CompilerProfileHash,
                "compiler_profile_hash",
                out Hash256? profileHash);
            if (profileError is not null || profileHash is null)
            {
                return ApplicationResults.Fail(profileError!);
            }

            compilerProfile = profileHash;
        }

        bool profileSupported = compilerProfile.Equals(RouterOsCompilerProfile.LayoutV1Hash);

        Node? node = await _nodes.GetAsync(new NodeId(command.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' was not found."));
        }

        PolicyAnalysisRun? run = await _approvals
            .GetAnalysisRunAsync(new PolicyAnalysisRunId(command.AnalysisRunId), cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Analysis run '{command.AnalysisRunId}' was not found."));
        }

        PolicyRevision? revision = await _policies
            .GetRevisionAsync(run.RevisionId, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Policy revision '{run.RevisionId}' was not found."));
        }

        bool analysisPassed = run.IsPass();
        bool analysisCurrent = run.DependencyFingerprint.Equals(currentFingerprint);
        bool inputApproved = revision.State == PolicyRevisionState.Approved
            && revision.ApprovedAnalysisRunId == run.Id
            && revision.ApprovedBundleHash is not null
            && revision.ApprovedBundleHash.Equals(run.BundleHash)
            && await HasActiveBindingForRunAsync(node, run, cancellationToken).ConfigureAwait(false);

        (ComposedEffectivePolicy? composed, ChainContractSet? contracts, ApplicationError? composeError) =
            await ComposeAsync(node, cancellationToken).ConfigureAwait(false);
        if (composeError is not null)
        {
            return ApplicationResults.Fail(composeError);
        }

        if (composed is null || contracts is null)
        {
            return CompileFail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Effective policy composition failed before compile.");
        }

        if (!composed.LogicalEffectiveHash.Equals(run.LogicalEffectiveHash))
        {
            return CompileFail(
                PolicyCompilerCodes.CompilerAnalysisStale,
                "Logical effective policy hash no longer matches the analysis run.");
        }

        PolicyDocument catalogDocument = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            chainContracts: contracts,
            addressObjects: composed.MergedAddressObjects,
            serviceObjects: composed.MergedServiceObjects,
            rules: composed.ActiveRules);
        PolicyObjectIdentity ownerTemplate = new(Guid.Empty, PolicyObjectOwnerScope.Company, null);
        if (!PolicyCatalogViewMapper.TryParseTypedAddresses(
                catalogDocument,
                ownerTemplate,
                out Dictionary<AddressObjectId, AddressObject> addresses,
                out string? addressError))
        {
            return CompileFail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                addressError ?? "Merged address catalogs are invalid.");
        }

        if (!PolicyCatalogViewMapper.TryParseTypedServices(
                catalogDocument,
                ownerTemplate,
                out Dictionary<ServiceObjectId, ServiceObject> services,
                out string? serviceError))
        {
            return CompileFail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                serviceError ?? "Merged service catalogs are invalid.");
        }

        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);
        if (devices.Count == 0)
        {
            return CompileFail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Node has no Devices to compile.");
        }

        IReadOnlyList<NodeZoneBinding> nodeBindings = await _bindings
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<ZoneId, NodeZoneBinding> bindingMap = nodeBindings.ToDictionary(static b => b.ZoneId);

        DateTimeOffset now = _clock.UtcNow;
        List<DeviceFilterCompileRequest> requests = [];
        foreach (Device device in devices.OrderBy(static d => d.Id.Value))
        {
            if (!device.Enabled)
            {
                continue;
            }

            ZoneResolveDeviceObservation observation = await _observations
                .GetForDeviceAsync(device.Id, cancellationToken)
                .ConfigureAwait(false);
            Hash256 capabilityHash;
            bool capabilityCurrent;
            if (device.LastCompletedCaptureId is null)
            {
                capabilityHash = Hash256.Create(new byte[Hash256.Size]);
                capabilityCurrent = false;
            }
            else
            {
                StoredSnapshot? snapshot = await _snapshots
                    .GetAsync(new SnapshotId(device.LastCompletedCaptureId.Value), cancellationToken)
                    .ConfigureAwait(false);
                if (snapshot?.Metadata.CapabilityHash is not { } capability)
                {
                    capabilityHash = Hash256.Create(new byte[Hash256.Size]);
                    capabilityCurrent = false;
                }
                else
                {
                    capabilityHash = capability.Value;
                    capabilityCurrent = capabilityHash.Equals(currentCapability);
                }
            }

            requests.Add(new DeviceFilterCompileRequest
            {
                DeviceId = device.Id,
                LogicalEffectivePolicyHash = composed.LogicalEffectiveHash,
                AnalysisBundleHash = run.BundleHash,
                CapabilityHash = capabilityHash,
                CompilerProfileHash = compilerProfile,
                AnalysisPassed = analysisPassed,
                InputApproved = inputApproved,
                AnalysisContextCurrent = analysisCurrent,
                CapabilityCurrent = capabilityCurrent,
                CompilerProfileSupported = profileSupported,
                ActiveRules = composed.ActiveRules,
                ChainContracts = contracts,
                Addresses = addresses,
                Services = services,
                Zones = new ZoneServiceCompileContext
                {
                    DeviceId = device.Id,
                    Bindings = bindingMap,
                    Observation = observation,
                    Services = services,
                    ActiveWanName = null,
                },
                CompiledAtUtc = now,
            });
        }

        if (requests.Count == 0)
        {
            return CompileFail(
                PolicyCompilerCodes.CompilerInputNotApproved,
                "Node has no enabled Devices to compile.");
        }

        NodeFilterCompileResult compiled = _compiler.CompileNode(requests);
        if (!compiled.IsSuccess)
        {
            return CompileFail(compiled.Code!, compiled.Message!);
        }

        List<FilterArtifactSummaryView> summaries = [];
        foreach (DeviceFilterCompileResult deviceResult in compiled.Devices)
        {
            RouterOsFilterArtifact artifact = deviceResult.Artifact!;
            CompilationProvenance provenance = deviceResult.Provenance!;
            StoredFilterArtifact stored = await _artifacts
                .PutIfAbsentAsync(artifact, provenance, cancellationToken)
                .ConfigureAwait(false);
            FilterArtifactSemanticSummary summary = deviceResult.Summary!;
            summaries.Add(new FilterArtifactSummaryView
            {
                DeviceId = summary.DeviceId.Value,
                ArtifactId = summary.ArtifactId,
                ResourceHash = summary.ResourceHash.Bytes.ToArray(),
                ResourceHashHex = summary.ResourceHash.ToString(),
                PhysicalSemanticsHash = summary.PhysicalSemanticsHash.Bytes.ToArray(),
                DeviceResolvedPolicyHash = summary.DeviceResolvedPolicyHash.Bytes.ToArray(),
                AnalysisBundleHash = summary.AnalysisBundleHash.Bytes.ToArray(),
                AddressListCount = summary.AddressListCount,
                ChainCount = summary.ChainCount,
                RuleCount = summary.RuleCount,
                AnchorTargetCount = summary.AnchorTargetCount,
                StoredAsNew = stored.Inserted,
            });
        }

        return ApplicationResults.Ok(new CompileNodeFilterArtifactsView
        {
            NodeId = node.Id.Value,
            LogicalEffectivePolicyHash = compiled.LogicalEffectivePolicyHash!.Bytes.ToArray(),
            LogicalEffectivePolicyHashHex = compiled.LogicalEffectivePolicyHash.ToString(),
            Artifacts = summaries,
        });
    }

    private async Task<bool> HasActiveBindingForRunAsync(
        Node node,
        PolicyAnalysisRun run,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyDesiredBinding> nodeBindings = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Node, node.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (nodeBindings.Any(b => b.AnalysisRunId == run.Id && b.BundleHash.Equals(run.BundleHash)))
        {
            return true;
        }

        IReadOnlyList<PolicyDesiredBinding> siteBindings = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Site, node.SiteId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (siteBindings.Any(b => b.AnalysisRunId == run.Id && b.BundleHash.Equals(run.BundleHash)))
        {
            return true;
        }

        IReadOnlyList<PolicyDesiredBinding> companyBindings = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Company, null, cancellationToken)
            .ConfigureAwait(false);
        return companyBindings.Any(b => b.AnalysisRunId == run.Id && b.BundleHash.Equals(run.BundleHash));
    }

    private async Task<(ComposedEffectivePolicy? Composed, ChainContractSet? Contracts, ApplicationError? Error)>
        ComposeAsync(Node node, CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> companies = await _policies
            .ListActiveByKindAsync(PolicyKind.CompanyBaseline, cancellationToken)
            .ConfigureAwait(false);
        if (companies.Count != 1)
        {
            return (null, null, new ApplicationError(
                PolicyComposeCodes.CompanyRequired,
                "Exactly one ACTIVE company baseline is required for compilation."));
        }

        (PolicyLayer? companyLayer, _, ApplicationError? companyError) =
            await LoadApprovedLayerAsync(companies[0], cancellationToken).ConfigureAwait(false);
        if (companyError is not null)
        {
            return (null, null, companyError);
        }

        if (companyLayer is null)
        {
            return (null, null, new ApplicationError(
                PolicyComposeCodes.CompanyRequired,
                "Company baseline has no APPROVED revision."));
        }

        (PolicyLayer? siteLayer, _, ApplicationError? siteError) =
            await LoadOptionalOverlayAsync(PolicyKind.SiteOverlay, node.SiteId.Value, cancellationToken)
                .ConfigureAwait(false);
        if (siteError is not null)
        {
            return (null, null, siteError);
        }

        (PolicyLayer? nodeLayer, _, ApplicationError? nodeError) =
            await LoadOptionalOverlayAsync(PolicyKind.NodeOverlay, node.Id.Value, cancellationToken)
                .ConfigureAwait(false);
        if (nodeError is not null)
        {
            return (null, null, nodeError);
        }

        (IReadOnlyList<PolicyLayer>? exceptionLayers, ApplicationError? exceptionError) =
            await LoadExceptionsAsync(node, cancellationToken).ConfigureAwait(false);
        if (exceptionError is not null)
        {
            return (null, null, exceptionError);
        }

        IReadOnlyList<ZoneDefinition> zones = await _zones.ListAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> knownZoneIds = zones.Select(static z => z.Id.Value).ToHashSet();

        PolicyComposeResult composed = EffectivePolicyComposer.Compose(
            companyLayer,
            siteLayer,
            nodeLayer,
            node.Id.Value,
            node.SiteId.Value,
            knownZoneIds,
            exceptionLayers);
        if (composed.IsFailure)
        {
            return (null, null, new ApplicationError(composed.Code!, composed.Message!));
        }

        return (composed.Value, companyLayer.PolicyDocument.ChainContracts, null);
    }

    private async Task<(PolicyLayer? Layer, PolicyRevisionRefView? Ref, ApplicationError? Error)>
        LoadOptionalOverlayAsync(PolicyKind kind, Guid ownerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> overlays = await _policies
            .ListActiveByOwnerAsync(kind, ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (overlays.Count == 0)
        {
            return (null, null, null);
        }

        if (overlays.Count != 1)
        {
            return (null, null, new ApplicationError(
                PolicyComposeCodes.PolicyNotUnique,
                $"Exactly one ACTIVE {kind} policy is allowed per owner; duplicates are forbidden."));
        }

        return await LoadApprovedLayerAsync(overlays[0], cancellationToken).ConfigureAwait(false);
    }

    private async Task<(IReadOnlyList<PolicyLayer>? Layers, ApplicationError? Error)> LoadExceptionsAsync(
        Node node,
        CancellationToken cancellationToken)
    {
        List<PolicyLayer> layers = [];
        ApplicationError? siteError = await AppendExceptionLayersAsync(
            node.SiteId.Value, layers, cancellationToken).ConfigureAwait(false);
        if (siteError is not null)
        {
            return (null, siteError);
        }

        ApplicationError? nodeError = await AppendExceptionLayersAsync(
            node.Id.Value, layers, cancellationToken).ConfigureAwait(false);
        if (nodeError is not null)
        {
            return (null, nodeError);
        }

        return (layers, null);
    }

    private async Task<ApplicationError?> AppendExceptionLayersAsync(
        Guid ownerId,
        List<PolicyLayer> layers,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Policy> policies = await _policies
            .ListActiveByOwnerAsync(PolicyKind.Exception, ownerId, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = _clock.UtcNow;
        foreach (Policy policy in policies)
        {
            (PolicyLayer? layer, _, ApplicationError? error) =
                await LoadApprovedLayerAsync(policy, cancellationToken).ConfigureAwait(false);
            if (error is not null)
            {
                return error;
            }

            if (layer is null)
            {
                continue;
            }

            ExceptionMetadata? metadata = layer.PolicyDocument.ExceptionMetadata;
            if (metadata is not null && metadata.IsExpired(now))
            {
                continue;
            }

            layers.Add(layer);
        }

        return null;
    }

    private async Task<(PolicyLayer? Layer, PolicyRevisionRefView? Ref, ApplicationError? Error)>
        LoadApprovedLayerAsync(Policy policy, CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyRevision> revisions = await _policies
            .ListRevisionsAsync(policy.Id, cancellationToken)
            .ConfigureAwait(false);
        PolicyRevision? approved = revisions
            .Where(static r => r.State == PolicyRevisionState.Approved)
            .OrderByDescending(static r => r.RevisionNumber)
            .FirstOrDefault();
        if (approved is null)
        {
            return (null, null, null);
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(approved);
        if (document.IsFailure)
        {
            return (null, null, document.Error);
        }

        PolicyLayer layer = new()
        {
            PolicyId = policy.Id.Value,
            RevisionId = approved.Id.Value,
            Kind = policy.Kind,
            OwnerScope = policy.OwnerScope,
            OwnerId = policy.OwnerId,
            ContentHash = approved.ContentHash,
            ParentContextHash = approved.ParentContextHash,
            PolicyDocument = document.Value!,
        };
        PolicyRevisionRefView refs = new()
        {
            PolicyId = policy.Id.Value,
            RevisionId = approved.Id.Value,
            RevisionNumber = approved.RevisionNumber,
            ContentHash = approved.ContentHash.Bytes.ToArray(),
            ContentHashHex = approved.ContentHash.ToString(),
        };
        return (layer, refs, null);
    }

    private static ApplicationFailure CompileFail(string code, string message)
        => ApplicationResults.Fail(new ApplicationError(code, message));
}
