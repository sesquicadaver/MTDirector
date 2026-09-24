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
/// Production calculator (AUDIT-AN-02 / AUDIT-AN-03): live dependency vector including
/// company/site/node bindings, active exceptions, compatibility, and management profile.
/// Never echoes <see cref="DependencyFingerprintRequest.FrozenRunFingerprint"/>.
/// </summary>
public sealed class LivePolicyDependencyFingerprintCalculator : IPolicyDependencyFingerprintCalculator
{
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly ISnapshotStore _snapshots;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IPolicyApprovalStore _approvals;

    public LivePolicyDependencyFingerprintCalculator(
        INodeStore nodes,
        IDeviceStore devices,
        ISnapshotStore snapshots,
        INodeZoneBindingStore bindings,
        IPolicyApprovalStore approvals)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(approvals);
        _nodes = nodes;
        _devices = devices;
        _snapshots = snapshots;
        _bindings = bindings;
        _approvals = approvals;
    }

    public async Task<Hash256> ComputeCurrentAsync(
        DependencyFingerprintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LiveSlots slots = request.NodeId is null
            ? await CollectBindingOnlySlotsAsync(cancellationToken).ConfigureAwait(false)
            : await CollectLiveSlotsAsync(request.NodeId.Value, cancellationToken).ConfigureAwait(false);
        return HashVector(slots, request);
    }

    private async Task<LiveSlots> CollectBindingOnlySlotsAsync(CancellationToken cancellationToken)
    {
        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        Hash256 company = await HashActiveBindingsAsync(PolicyBindingScope.Company, null, cancellationToken)
            .ConfigureAwait(false);
        return new LiveSlots(
            Company: company,
            Site: empty,
            Node: empty,
            Exceptions: empty,
            Zone: empty,
            Membership: empty,
            Configuration: empty,
            Capability: empty,
            Compatibility: empty,
            Management: empty,
            AnchorGuard: empty);
    }

    private async Task<LiveSlots> CollectLiveSlotsAsync(NodeId nodeId, CancellationToken cancellationToken)
    {
        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return await CollectBindingOnlySlotsAsync(cancellationToken).ConfigureAwait(false);
        }

        Hash256 company = await HashActiveBindingsAsync(PolicyBindingScope.Company, null, cancellationToken)
            .ConfigureAwait(false);
        Hash256 site = await HashActiveBindingsAsync(
                PolicyBindingScope.Site, node.SiteId.Value, cancellationToken)
            .ConfigureAwait(false);
        Hash256 nodeBinding = await HashActiveBindingsAsync(
                PolicyBindingScope.Node, node.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PolicyDesiredBinding> siteExceptions = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Exception, node.SiteId.Value, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PolicyDesiredBinding> nodeExceptions = await _approvals
            .ListActiveBindingsAsync(PolicyBindingScope.Exception, node.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        Hash256 exceptions = HashOrdered(
            siteExceptions.Concat(nodeExceptions)
                .OrderBy(static b => b.Id.Value)
                .Select(HashDesiredBinding));

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
        IOrderedEnumerable<Device> enabled = devices
            .Where(static d => d.Enabled)
            .OrderBy(static d => d.Id.Value);
        Hash256 membership = HashOrdered(
            enabled.Select(static d => Hash256.Create(SHA256.HashData(d.Id.Value.ToByteArray()))));

        List<Hash256> configs = [];
        List<Hash256> capabilities = [];
        List<Hash256> compatibility = [];
        List<Hash256> management = [];
        foreach (Device device in enabled)
        {
            management.Add(HashManagementEndpoint(device));
            compatibility.Add(HashCompatibility(device));
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
            Company: company,
            Site: site,
            Node: nodeBinding,
            Exceptions: exceptions,
            Zone: zone,
            Membership: membership,
            Configuration: configuration,
            Capability: capability,
            Compatibility: HashOrdered(compatibility),
            Management: HashOrdered(management),
            AnchorGuard: HashOrdered([configuration, capability]));
    }

    private async Task<Hash256> HashActiveBindingsAsync(
        PolicyBindingScope scope,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyDesiredBinding> bindings = await _approvals
            .ListActiveBindingsAsync(scope, scopeId, cancellationToken)
            .ConfigureAwait(false);
        return HashOrdered(bindings.OrderBy(static b => b.Id.Value).Select(HashDesiredBinding));
    }

    private static Hash256 HashDesiredBinding(PolicyDesiredBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, "mfc.policy.desired_binding.v1");
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)binding.Scope]);
        hasher.AppendData(binding.ScopeId?.ToByteArray() ?? new byte[16]);
        hasher.AppendData(binding.PolicyId.Value.ToByteArray());
        hasher.AppendData(binding.DesiredRevisionId.Value.ToByteArray());
        hasher.AppendData(binding.AnalysisRunId.Value.ToByteArray());
        hasher.AppendData(binding.BundleHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static Hash256 HashManagementEndpoint(Device device)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, "mfc.policy.management_profile.v1");
        hasher.AppendData([(byte)0]);
        hasher.AppendData(device.Id.Value.ToByteArray());
        AppendUtf8(hasher, device.ManagementEndpoint.Host.Value);
        hasher.AppendData([(byte)0]);
        Span<byte> port = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(port, device.ManagementEndpoint.Port);
        hasher.AppendData(port);
        hasher.AppendData([(byte)device.Role]);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static Hash256 HashCompatibility(Device device)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, "mfc.policy.compatibility.v1");
        hasher.AppendData([(byte)0]);
        hasher.AppendData(device.Id.Value.ToByteArray());
        AppendUtf8(hasher, device.LastSupportState?.ToString() ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, device.LastObservedReachability?.ToString() ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, device.ManagementState.ToString());
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static Hash256 HashVector(LiveSlots slots, DependencyFingerprintRequest request)
        => PolicyApprovalHasher.HashDependencyFingerprint(new PolicyApprovalDependencyVector
        {
            CompanyBindingHash = slots.Company,
            SiteBindingHash = slots.Site,
            NodeBindingHash = slots.Node,
            ActiveExceptionsHash = slots.Exceptions,
            ZoneBindingHash = slots.Zone,
            NodeMembershipHash = slots.Membership,
            RouterOsConfigurationHash = slots.Configuration,
            CapabilityHash = slots.Capability,
            CompatibilityHash = slots.Compatibility,
            ManagementAccessProfileHash = slots.Management,
            AnchorGuardContextHash = slots.AnchorGuard,
            AnalyzerVersion = request.AnalyzerVersion,
            PolicySchemaVersion = request.PolicySchemaVersion,
            PipelineVersion = request.PipelineVersion,
        });

    private static Hash256 HashOrdered(IEnumerable<Hash256> digests)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, "mfc.policy.live_deps.v1");
        hasher.AppendData([(byte)0]);
        foreach (Hash256 digest in digests)
        {
            ArgumentNullException.ThrowIfNull(digest);
            hasher.AppendData(digest.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private readonly record struct LiveSlots(
        Hash256 Company,
        Hash256 Site,
        Hash256 Node,
        Hash256 Exceptions,
        Hash256 Zone,
        Hash256 Membership,
        Hash256 Configuration,
        Hash256 Capability,
        Hash256 Compatibility,
        Hash256 Management,
        Hash256 AnchorGuard);
}
