using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Workflow;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Deployment;

/// <summary>One sealed compile artifact reference supplied by Desktop after CompileNodeFilterArtifacts.</summary>
public sealed class SealedArtifactDeviceRef
{
    public required Guid DeviceId { get; init; }

    public required byte[] NewArtifactResourceHash { get; init; }
}

/// <summary>Create deployment plan from Controller-sealed filter artifacts (AUDIT-GUI-01).</summary>
public sealed class CreateDeploymentPlanFromSealedArtifactsCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required IReadOnlyList<SealedArtifactDeviceRef> Devices { get; init; }
}

/// <summary>Plan summary plus capture-derived packet-path pairs for Start (no fabricated interfaces).</summary>
public sealed class SealedDeploymentPlanResult
{
    public required DeploymentPlanSummaryView Plan { get; init; }

    public required IReadOnlyList<PacketPathPairFact> CapturePacketPathPairs { get; init; }
}

/// <summary>
/// Builds DeviceDeploymentPlans from sealed filter-artifact store + last capture / hash state,
/// then persists via <see cref="CreateDeploymentPlanUseCase"/> (AUDIT-GUI-01).
/// </summary>
public sealed class CreateDeploymentPlanFromSealedArtifactsUseCase
{
    public const string Operation = "deployment.create_plan_from_sealed";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly IPolicyApprovalStore _approvals;
    private readonly IFilterArtifactStore _artifacts;
    private readonly IDeviceHashStateStore _hashStates;
    private readonly ISnapshotStore _snapshots;
    private readonly CreateDeploymentPlanUseCase _createPlan;

    public CreateDeploymentPlanFromSealedArtifactsUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        IPolicyApprovalStore approvals,
        IFilterArtifactStore artifacts,
        IDeviceHashStateStore hashStates,
        ISnapshotStore snapshots,
        CreateDeploymentPlanUseCase createPlan)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(approvals);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(hashStates);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(createPlan);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _approvals = approvals;
        _artifacts = artifacts;
        _hashStates = hashStates;
        _snapshots = snapshots;
        _createPlan = createPlan;
    }

    public async Task<ApplicationResult<SealedDeploymentPlanResult>> ExecuteAsync(
        CreateDeploymentPlanFromSealedArtifactsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DeploymentWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        if (command.Devices.Count == 0)
        {
            return ApplicationResults.Fail(
                ApplicationError.Validation("CreatePlanFromSealedArtifacts requires at least one sealed device artifact."));
        }

        Node? node = await _nodes.GetAsync(new NodeId(command.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
        }

        PolicyAnalysisRun? run = await _approvals
            .GetAnalysisRunAsync(new PolicyAnalysisRunId(command.AnalysisRunId), cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Analysis run '{command.AnalysisRunId}' was not found."));
        }

        IReadOnlyList<Device> nodeDevices = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> nodeDeviceIds = nodeDevices.Select(static d => d.Id.Value).ToHashSet();
        Dictionary<Guid, Device> deviceById = nodeDevices.ToDictionary(static d => d.Id.Value);

        List<DeviceDeploymentPlan> devicePlans = [];
        List<PacketPathPairFact> packetPaths = [];
        try
        {
            foreach (SealedArtifactDeviceRef deviceRef in command.Devices.OrderBy(static d => d.DeviceId))
            {
                if (!nodeDeviceIds.Contains(deviceRef.DeviceId))
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(
                        $"Device '{deviceRef.DeviceId:D}' is not a member of node '{command.NodeId:D}'."));
                }

                ApplicationError? hashError = PolicyRevisionSupport.TryHash(
                    deviceRef.NewArtifactResourceHash,
                    "new_artifact_resource_hash",
                    out Hash256? newHash);
                if (hashError is not null || newHash is null)
                {
                    return ApplicationResults.Fail(hashError!);
                }

                StoredFilterArtifact? meta = await _artifacts
                    .GetByResourceHashAsync(newHash, cancellationToken)
                    .ConfigureAwait(false);
                if (meta is null)
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(
                        $"{DeploymentCodes.ActiveArtifactHashMismatch}: sealed filter artifact metadata missing."));
                }

                byte[]? canonical = await _artifacts
                    .GetCanonicalBytesByResourceHashAsync(newHash, cancellationToken)
                    .ConfigureAwait(false);
                if (canonical is null || canonical.Length == 0)
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(
                        $"{DeploymentCodes.ActiveArtifactHashMismatch}: sealed filter artifact body missing."));
                }

                RouterOsFilterArtifactReader.ParsedBody newBody = RouterOsFilterArtifactReader.Read(canonical);
                DeviceHashState? hashState = await _hashStates
                    .GetAsync(new DeviceId(deviceRef.DeviceId), cancellationToken)
                    .ConfigureAwait(false);

                RouterOsFilterArtifactReader.ParsedBody? oldBody = null;
                if (hashState?.LastCommittedArtifactHash is { } oldHash
                    && !oldHash.Equals(BootstrapArtifact.Hash))
                {
                    byte[]? oldCanonical = await _artifacts
                        .GetCanonicalBytesByResourceHashAsync(oldHash, cancellationToken)
                        .ConfigureAwait(false);
                    if (oldCanonical is { Length: > 0 })
                    {
                        oldBody = RouterOsFilterArtifactReader.Read(oldCanonical);
                    }
                }

                Device device = deviceById[deviceRef.DeviceId];
                (string version, ConfigurationHash? cfg, IReadOnlyList<PacketPathPairFact> pairs) =
                    await LoadCaptureFactsAsync(device, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(version))
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(
                        $"Device '{deviceRef.DeviceId:D}' lacks RouterOS version from last completed capture."));
                }

                if (cfg is null)
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(
                        $"{DeploymentCodes.SealedEvidenceMissing}: Device '{deviceRef.DeviceId:D}' lacks configuration hash from last completed capture."));
                }

                DeploymentProbe apiSsl;
                try
                {
                    apiSsl = SealedDeploymentPlanBuilder.RequireApiSslProbe(device.ManagementEndpoint);
                }
                catch (DomainInvariantException ex)
                {
                    return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
                }

                devicePlans.Add(SealedDeploymentPlanBuilder.Build(
                    device.Id,
                    version,
                    meta,
                    newBody,
                    hashState,
                    oldBody,
                    cfg.Value,
                    [apiSsl]));
                foreach (PacketPathPairFact pair in pairs)
                {
                    packetPaths.Add(pair);
                }
            }
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }

        ApplicationResult<DeploymentPlanSummaryView> created = await _createPlan.ExecuteAsync(
            new CreateDeploymentPlanCommand
            {
                Actor = command.Actor,
                IdempotencyKey = command.IdempotencyKey,
                NodeId = command.NodeId,
                LogicalPolicyHash = run.LogicalEffectiveHash.Bytes.ToArray(),
                AnalysisBundleHash = run.BundleHash.Bytes.ToArray(),
                TopologyProjectionHash = run.TopologyProjectionHash.Bytes.ToArray(),
                DevicePlans = devicePlans,
            },
            cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ApplicationResults.Fail(created.Error!);
        }

        return ApplicationResults.Ok(new SealedDeploymentPlanResult
        {
            Plan = created.Value!,
            CapturePacketPathPairs = DeduplicatePairs(packetPaths),
        });
    }

    private async Task<(string Version, ConfigurationHash? Configuration, IReadOnlyList<PacketPathPairFact> Pairs)>
        LoadCaptureFactsAsync(Device device, CancellationToken cancellationToken)
    {
        if (device.LastCompletedCaptureId is not Guid captureId)
        {
            return (string.Empty, null, []);
        }

        StoredSnapshot? snapshot = await _snapshots
            .GetAsync(new SnapshotId(captureId), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<CanonicalSection> sections = await _snapshots
            .LoadCanonicalSectionsAsync(new SnapshotId(captureId), cancellationToken)
            .ConfigureAwait(false);
        DeviceLastCaptureFacts facts = DeviceLastCaptureFacts.FromCanonicalSections(sections);
        List<CanonicalRecord> pairRecords = [];
        foreach (CanonicalSection section in sections)
        {
            if (section.SectionId is CanonicalSectionIds.TopologyValidation
                or CanonicalSectionIds.TopologyContainerVeth)
            {
                pairRecords.AddRange(section.Records);
            }
        }

        IReadOnlyList<PacketPathPairFact> pairs = PacketPathContextMapper.FromCanonicalPairs(pairRecords);
        return (facts.RouterOsVersion ?? string.Empty, snapshot?.Metadata.ConfigurationHash, pairs);
    }

    private static List<PacketPathPairFact> DeduplicatePairs(IReadOnlyList<PacketPathPairFact> pairs)
    {
        List<PacketPathPairFact> unique = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (PacketPathPairFact pair in pairs)
        {
            string key = $"{pair.IngressInterface}\0{pair.EgressInterface}\0{pair.PathClass}";
            if (seen.Add(key))
            {
                unique.Add(pair);
            }
        }

        return unique;
    }
}
