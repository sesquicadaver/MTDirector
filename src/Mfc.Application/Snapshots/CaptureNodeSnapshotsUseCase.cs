using System.Security.Cryptography;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Snapshots;

public sealed class CaptureNodeSnapshotsCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    /// <summary>Client key for the Node batch; per-device keys are derived from this + DeviceId.</summary>
    public required Guid IdempotencyKey { get; init; }
}

public sealed class CaptureNodeMemberSnapshotView
{
    public required Guid DeviceId { get; init; }

    public required string DisplayName { get; init; }

    public required SnapshotView Snapshot { get; init; }
}

public sealed class CaptureNodeSnapshotsView
{
    public required Guid NodeId { get; init; }

    public required IReadOnlyList<CaptureNodeMemberSnapshotView> Members { get; init; }
}

/// <summary>
/// Captures every Device on a Node (W6-03 / StartCapture node_id).
/// Does not invent VRRP roles; does not WriteEnabled; CompareSnapshots a↔b unchanged.
/// </summary>
public sealed class CaptureNodeSnapshotsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly CaptureSnapshotUseCase _captureDevice;

    public CaptureNodeSnapshotsUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        CaptureSnapshotUseCase captureDevice)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(captureDevice);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _captureDevice = captureDevice;
    }

    public async Task<ApplicationResult<CaptureNodeSnapshotsView>> ExecuteAsync(
        CaptureNodeSnapshotsCommand command,
        Func<Guid, string, CancellationToken, Task>? onMemberStarted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.SnapshotCapture, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        if (command.IdempotencyKey == Guid.Empty)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed("IdempotencyKey must be a non-empty GUID."));
        }

        Node? node = await _nodes.GetAsync(new NodeId(command.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
        }

        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);
        if (devices.Count == 0)
        {
            return ApplicationResults.Fail(
                ApplicationError.Validation($"Node '{command.NodeId}' has no devices to capture."));
        }

        List<CaptureNodeMemberSnapshotView> members = new(devices.Count);
        foreach (Device device in devices.OrderBy(static d => d.DisplayName.Value, StringComparer.Ordinal))
        {
            if (onMemberStarted is not null)
            {
                await onMemberStarted(device.Id.Value, device.DisplayName.Value, cancellationToken)
                    .ConfigureAwait(false);
            }

            Guid deviceKey = DeriveDeviceIdempotencyKey(command.IdempotencyKey, device.Id.Value);
            ApplicationResult<SnapshotView> captured = await _captureDevice
                .ExecuteAsync(
                    new CaptureSnapshotCommand
                    {
                        Actor = command.Actor,
                        DeviceId = device.Id.Value,
                        IdempotencyKey = deviceKey,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!captured.IsSuccess)
            {
                return ApplicationResults.Fail(captured.Error!);
            }

            members.Add(new CaptureNodeMemberSnapshotView
            {
                DeviceId = device.Id.Value,
                DisplayName = device.DisplayName.Value,
                Snapshot = captured.Value!,
            });
        }

        return ApplicationResults.Ok(new CaptureNodeSnapshotsView
        {
            NodeId = node.Id.Value,
            Members = members,
        });
    }

    /// <summary>Stable per-device key so one Node batch key does not collide across members.</summary>
    public static Guid DeriveDeviceIdempotencyKey(Guid nodeBatchKey, Guid deviceId)
    {
        Span<byte> material = stackalloc byte[32];
        nodeBatchKey.TryWriteBytes(material);
        deviceId.TryWriteBytes(material[16..]);
        byte[] hash = SHA256.HashData(material);
        return new Guid(hash.AsSpan(0, 16));
    }
}
