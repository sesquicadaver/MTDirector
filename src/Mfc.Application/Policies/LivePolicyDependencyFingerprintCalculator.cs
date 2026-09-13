using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Policies;

/// <summary>
/// Production calculator (AUDIT-AN-02): Node-scoped live fingerprint; Approve/Bind without Node echoes frozen run FP.
/// </summary>
public sealed class LivePolicyDependencyFingerprintCalculator : IPolicyDependencyFingerprintCalculator
{
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly ISnapshotStore _snapshots;
    private readonly INodeZoneBindingStore _bindings;

    public LivePolicyDependencyFingerprintCalculator(
        INodeStore nodes,
        IDeviceStore devices,
        ISnapshotStore snapshots,
        INodeZoneBindingStore bindings)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(bindings);
        _nodes = nodes;
        _devices = devices;
        _snapshots = snapshots;
        _bindings = bindings;
    }

    public async Task<Hash256> ComputeCurrentAsync(
        DependencyFingerprintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.NodeId is null)
        {
            // Approve/Bind: device/zone slots are validated at Compile; echo frozen run FP for CAS.
            if (request.FrozenRunFingerprint is not null)
            {
                return request.FrozenRunFingerprint;
            }

            return HashVector(EmptySlots(), request);
        }

        LiveSlots slots = await CollectLiveSlotsAsync(request.NodeId.Value, cancellationToken).ConfigureAwait(false);
        return HashVector(slots, request);
    }

    private async Task<LiveSlots> CollectLiveSlotsAsync(NodeId nodeId, CancellationToken cancellationToken)
    {
        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return EmptySlots();
        }

        IReadOnlyList<NodeZoneBinding> zoneBindings = await _bindings
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);
        Hash256 zone = HashOrdered(
            zoneBindings
                .OrderBy(static b => b.ZoneId.Value)
                .Select(static b => NodeZoneBinding.ComputeDependencyHash(b.Kind, b.Values, resolvedMembers: [])));

        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);
        Hash256 membership = HashOrdered(
            devices
                .Where(static d => d.Enabled)
                .OrderBy(static d => d.Id.Value)
                .Select(static d => Hash256.Create(SHA256.HashData(d.Id.Value.ToByteArray()))));

        List<Hash256> configs = [];
        List<Hash256> capabilities = [];
        foreach (Device device in devices.Where(static d => d.Enabled).OrderBy(static d => d.Id.Value))
        {
            if (device.LastCompletedCaptureId is null)
            {
                configs.Add(empty);
                capabilities.Add(empty);
                continue;
            }

            StoredSnapshot? snapshot = await _snapshots
                .GetAsync(new SnapshotId(device.LastCompletedCaptureId.Value), cancellationToken)
                .ConfigureAwait(false);
            configs.Add(snapshot?.Metadata.ConfigurationHash is { } cfg ? cfg.Value : empty);
            capabilities.Add(snapshot?.Metadata.CapabilityHash is { } cap ? cap.Value : empty);
        }

        Hash256 configuration = HashOrdered(configs);
        Hash256 capability = HashOrdered(capabilities);
        return new LiveSlots(
            zone,
            membership,
            configuration,
            capability,
            AnchorGuard: HashOrdered([configuration, capability]));
    }

    private static LiveSlots EmptySlots()
    {
        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        return new LiveSlots(empty, empty, empty, empty, empty);
    }

    private static Hash256 HashVector(LiveSlots slots, DependencyFingerprintRequest request)
    {
        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        return PolicyApprovalHasher.HashDependencyFingerprint(new PolicyApprovalDependencyVector
        {
            CompanyBindingHash = empty,
            SiteBindingHash = empty,
            NodeBindingHash = empty,
            ActiveExceptionsHash = empty,
            ZoneBindingHash = slots.Zone,
            NodeMembershipHash = slots.Membership,
            RouterOsConfigurationHash = slots.Configuration,
            CapabilityHash = slots.Capability,
            CompatibilityHash = empty,
            ManagementAccessProfileHash = empty,
            AnchorGuardContextHash = slots.AnchorGuard,
            AnalyzerVersion = request.AnalyzerVersion,
            PolicySchemaVersion = request.PolicySchemaVersion,
            PipelineVersion = request.PipelineVersion,
        });
    }

    private static Hash256 HashOrdered(IEnumerable<Hash256> digests)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes("mfc.policy.live_deps.v1"));
        hasher.AppendData([(byte)0]);
        foreach (Hash256 digest in digests)
        {
            ArgumentNullException.ThrowIfNull(digest);
            hasher.AppendData(digest.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private readonly record struct LiveSlots(
        Hash256 Zone,
        Hash256 Membership,
        Hash256 Configuration,
        Hash256 Capability,
        Hash256 AnchorGuard);
}
