using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Onboarding;

/// <summary>
/// AUDIT-GUI-02 / F11: builds an onboarding plan from each Device's last completed capture.
/// Desktop supplies only Node + idempotency; Controller owns hashes and device plans.
/// </summary>
public sealed class CreateOnboardingPlanFromLastCaptureCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid NodeId { get; init; }
}

/// <summary>Creates an onboarding plan from Controller-owned last-capture facts.</summary>
public sealed class CreateOnboardingPlanFromLastCaptureUseCase
{
    private readonly CreateOnboardingPlanUseCase _createPlan;
    private readonly INodeStore _nodes;
    private readonly ISnapshotStore _snapshots;
    private readonly IAuthorizationBoundary _auth;

    public CreateOnboardingPlanFromLastCaptureUseCase(
        CreateOnboardingPlanUseCase createPlan,
        INodeStore nodes,
        ISnapshotStore snapshots,
        IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(createPlan);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(auth);
        _createPlan = createPlan;
        _nodes = nodes;
        _snapshots = snapshots;
        _auth = auth;
    }

    public async Task<ApplicationResult<OnboardingPlanSummaryView>> ExecuteAsync(
        CreateOnboardingPlanFromLastCaptureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.OnboardingWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Node? node = await _nodes.GetAsync(new NodeId(command.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' was not found."));
        }

        Device[] enabled = [.. node.Devices.Where(static d => d.Enabled).OrderBy(static d => d.Id.Value)];
        if (enabled.Length == 0)
        {
            return ApplicationResults.Fail(new ApplicationError(
                OnboardingCodes.DevicePlanCardinality,
                "Node has no enabled Devices for an onboarding plan."));
        }

        List<DeviceOnboardingPlan> devicePlans = [];
        using IncrementalHash membership = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash topology = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (Device device in enabled)
        {
            if (device.LastCompletedCaptureId is not Guid captureId)
            {
                return ApplicationResults.Fail(new ApplicationError(
                    OnboardingCodes.CaptureRequired,
                    $"Device '{device.Id.Value}' has no last completed capture for Controller-built onboarding."));
            }

            StoredSnapshot? snapshot = await _snapshots.GetAsync(new SnapshotId(captureId), cancellationToken)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound(
                    $"Snapshot '{captureId}' for device '{device.Id.Value}' was not found."));
            }

            if (snapshot.Metadata.CapabilityHash is null || snapshot.Metadata.ConfigurationHash is null)
            {
                return ApplicationResults.Fail(new ApplicationError(
                    OnboardingCodes.CaptureRequired,
                    $"Snapshot '{captureId}' is missing capability/configuration hashes."));
            }

            IReadOnlyList<Domain.Canonicalization.CanonicalSection> sections = await _snapshots
                .LoadCanonicalSectionsAsync(new SnapshotId(captureId), cancellationToken)
                .ConfigureAwait(false);
            DeviceLastCaptureFacts facts = DeviceLastCaptureFacts.FromCanonicalSections(sections);
            string version = string.IsNullOrWhiteSpace(facts.RouterOsVersion)
                ? "0.0.0"
                : facts.RouterOsVersion!;

            Hash256 captureSalt = Hash256.Create(SHA256.HashData(
                Encoding.UTF8.GetBytes($"mfc.onboarding.capture:{captureId:D}")));
            Hash256 observation = snapshot.Metadata.ObservationHash is { } obs
                ? obs.Value
                : captureSalt;

            IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(node.DeclaredKind, includeIpv6: false);
            List<AnchorPlacement> placements = [];
            uint ordinal = 0;
            foreach (AnchorKey key in keys)
            {
                placements.Add(AnchorPlacement.Create(
                    key.Family,
                    key.Chain,
                    AnchorPlacementMode.Append,
                    expectedAnchorOrdinal: ordinal));
                ordinal++;
            }

            devicePlans.Add(DeviceOnboardingPlan.Create(
                device.Id,
                version,
                snapshot.Metadata.CapabilityHash.Value.Value,
                snapshot.Metadata.ConfigurationHash.Value.Value,
                observation,
                SlotHash(captureSalt, "api"),
                SlotHash(captureSalt, "read"),
                SlotHash(captureSalt, "deploy"),
                SlotHash(captureSalt, "mode"),
                SlotHash(captureSalt, "guard"),
                keys,
                placements,
                BootstrapArtifact.Hash,
                OnboardingCodes.DefaultWatchdogTtl));

            membership.AppendData(device.Id.Value.ToByteArray());
            topology.AppendData(observation.Bytes);
        }

        return await _createPlan.ExecuteAsync(
                new CreateOnboardingPlanCommand
                {
                    Actor = command.Actor,
                    IdempotencyKey = command.IdempotencyKey,
                    NodeId = command.NodeId,
                    NodeMembershipHash = Hash256.Create(membership.GetHashAndReset()).Bytes.ToArray(),
                    TopologyProjectionHash = Hash256.Create(topology.GetHashAndReset()).Bytes.ToArray(),
                    DevicePlans = devicePlans,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Hash256 SlotHash(Hash256 captureSalt, string slot)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(captureSalt.Bytes);
        hasher.AppendData(Encoding.UTF8.GetBytes(slot));
        return Hash256.Create(hasher.GetHashAndReset());
    }
}
