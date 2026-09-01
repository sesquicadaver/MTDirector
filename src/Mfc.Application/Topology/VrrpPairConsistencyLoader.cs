using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Topology;
using Mfc.Domain.Workflow;

namespace Mfc.Application.Topology;

/// <summary>
/// Loads last-capture sections + desired logical hashes for Node members and runs
/// <see cref="VrrpPairConsistencyAnalyzer"/> (no authorization — callers gate first).
/// </summary>
public sealed class VrrpPairConsistencyLoader
{
    private readonly IDeviceStore _devices;
    private readonly ISnapshotStore _snapshots;
    private readonly IDeviceHashStateStore _hashStates;

    public VrrpPairConsistencyLoader(
        IDeviceStore devices,
        ISnapshotStore snapshots,
        IDeviceHashStateStore hashStates)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(hashStates);
        _devices = devices;
        _snapshots = snapshots;
        _hashStates = hashStates;
    }

    public async Task<VrrpPairConsistencyResult> AnalyzeNodeAsync(
        Node node,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);

        List<VrrpPairMemberInput> members = new(devices.Count);
        foreach (Device device in devices.OrderBy(static d => d.DisplayName.Value, StringComparer.Ordinal))
        {
            IReadOnlyList<CanonicalSection> sections = [];
            if (device.LastCompletedCaptureId is Guid captureId)
            {
                sections = await _snapshots
                    .LoadCanonicalSectionsAsync(new SnapshotId(captureId), cancellationToken)
                    .ConfigureAwait(false);
            }

            DeviceHashState? hashState = await _hashStates
                .GetAsync(device.Id, cancellationToken)
                .ConfigureAwait(false);

            members.Add(new VrrpPairMemberInput
            {
                DeviceId = device.Id,
                DisplayName = device.DisplayName.Value,
                Sections = sections,
                DesiredLogicalHashHex = hashState?.DesiredPolicyHash?.ToString(),
            });
        }

        return VrrpPairConsistencyAnalyzer.Analyze(node, members);
    }
}
