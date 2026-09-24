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

    /// <summary>Completed capture when the member succeeded; null on failure (AUDIT-CAP-04).</summary>
    public SnapshotView? Snapshot { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class CaptureNodeSnapshotsView
{
    public required Guid NodeId { get; init; }

    public required IReadOnlyList<CaptureNodeMemberSnapshotView> Members { get; init; }

    /// <summary>
    /// True when every member succeeded and their CompletedAtUtc span is within
    /// <see cref="CaptureNodeSnapshotsUseCase.MaxMemberCaptureSkew"/>.
    /// </summary>
    public required bool TimeSetFit { get; init; }

    /// <summary>max(CompletedAt) − min(CompletedAt) among successful members, when known.</summary>
    public TimeSpan? ObservedCaptureSkew { get; init; }

    public bool AllMembersSucceeded =>
        Members.Count > 0 && Members.All(static m => m.Snapshot is not null && m.ErrorCode is null);
}

/// <summary>
/// Captures every Device on a Node (W6-03 / StartCapture node_id).
/// AUDIT-CAP-04 / F09: attempts all members (does not abort on first failure) and reports time-set fitness.
/// Does not invent VRRP roles; does not WriteEnabled; CompareSnapshots a↔b unchanged.
/// </summary>
public sealed class CaptureNodeSnapshotsUseCase
{
    /// <summary>Maximum allowed span of member CompletedAtUtc for a fit time-set (aligned with MaxRouterClockSkew).</summary>
    public static readonly TimeSpan MaxMemberCaptureSkew = TimeSpan.FromMinutes(5);

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

            if (captured.IsSuccess)
            {
                members.Add(new CaptureNodeMemberSnapshotView
                {
                    DeviceId = device.Id.Value,
                    DisplayName = device.DisplayName.Value,
                    Snapshot = captured.Value!,
                });
            }
            else
            {
                members.Add(new CaptureNodeMemberSnapshotView
                {
                    DeviceId = device.Id.Value,
                    DisplayName = device.DisplayName.Value,
                    ErrorCode = captured.Error!.Code,
                    ErrorMessage = captured.Error.Message,
                });
            }
        }

        bool timeSetFit = EvaluateTimeSetFit(members, out TimeSpan? observedSkew);
        return ApplicationResults.Ok(new CaptureNodeSnapshotsView
        {
            NodeId = node.Id.Value,
            Members = members,
            TimeSetFit = timeSetFit,
            ObservedCaptureSkew = observedSkew,
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

    /// <summary>
    /// Time-set is fit only when every member succeeded and CompletedAtUtc span ≤ MaxMemberCaptureSkew.
    /// </summary>
    public static bool EvaluateTimeSetFit(
        IReadOnlyList<CaptureNodeMemberSnapshotView> members,
        out TimeSpan? observedSkew)
    {
        ArgumentNullException.ThrowIfNull(members);
        observedSkew = null;
        if (members.Count == 0 || members.Any(static m => m.Snapshot is null))
        {
            return false;
        }

        List<DateTimeOffset> times = members
            .Select(static m => m.Snapshot!.CompletedAtUtc)
            .Where(static t => t.HasValue)
            .Select(static t => t!.Value)
            .ToList();
        if (times.Count != members.Count)
        {
            return false;
        }

        if (times.Count == 1)
        {
            observedSkew = TimeSpan.Zero;
            return true;
        }

        DateTimeOffset min = times.Min();
        DateTimeOffset max = times.Max();
        TimeSpan skew = max - min;
        observedSkew = skew;
        return skew <= MaxMemberCaptureSkew;
    }
}
