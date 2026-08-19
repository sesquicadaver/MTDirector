using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>Per-device deployment state machine (Safe Deployment Spec §14).</summary>
public sealed class DeviceDeployment
{
    private static readonly HashSet<(DeviceDeploymentState From, DeviceDeploymentState To)> Allowed =
    [
        (DeviceDeploymentState.Pending, DeviceDeploymentState.Prechecked),
        (DeviceDeploymentState.Prechecked, DeviceDeploymentState.Staging),
        (DeviceDeploymentState.Staging, DeviceDeploymentState.Staged),
        (DeviceDeploymentState.Staged, DeviceDeploymentState.WatchdogArmed),
        (DeviceDeploymentState.WatchdogArmed, DeviceDeploymentState.Activating),
        (DeviceDeploymentState.Activating, DeviceDeploymentState.ActiveUnverified),
        (DeviceDeploymentState.ActiveUnverified, DeviceDeploymentState.Verified),
        (DeviceDeploymentState.Verified, DeviceDeploymentState.WatchdogDisarmed),
        (DeviceDeploymentState.WatchdogDisarmed, DeviceDeploymentState.Committed),
        (DeviceDeploymentState.Staging, DeviceDeploymentState.RollingBack),
        (DeviceDeploymentState.WatchdogArmed, DeviceDeploymentState.RollingBack),
        (DeviceDeploymentState.Activating, DeviceDeploymentState.RollingBack),
        (DeviceDeploymentState.ActiveUnverified, DeviceDeploymentState.RollingBack),
        (DeviceDeploymentState.Verified, DeviceDeploymentState.RollingBack),
        (DeviceDeploymentState.RollingBack, DeviceDeploymentState.RolledBack),
        (DeviceDeploymentState.RollingBack, DeviceDeploymentState.RecoveryRequired),
        (DeviceDeploymentState.Staging, DeviceDeploymentState.RecoveryRequired),
        (DeviceDeploymentState.Activating, DeviceDeploymentState.RecoveryRequired),
    ];

    private DeviceDeployment(
        DeploymentOperationId operationId,
        DeviceId deviceId,
        DeviceDeploymentState state,
        DateTimeOffset updatedAtUtc)
    {
        OperationId = operationId;
        DeviceId = deviceId;
        State = state;
        UpdatedAtUtc = updatedAtUtc;
    }

    public DeploymentOperationId OperationId { get; }

    public DeviceId DeviceId { get; }

    public DeviceDeploymentState State { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsTerminal
        => State is DeviceDeploymentState.Committed
            or DeviceDeploymentState.RolledBack
            or DeviceDeploymentState.RecoveryRequired;

    public static DeviceDeployment Create(DeploymentOperationId operationId, DeviceId deviceId, DateTimeOffset nowUtc)
        => new(operationId, deviceId, DeviceDeploymentState.Pending, nowUtc.ToUniversalTime());

    public static DeviceDeployment Reconstitute(
        DeploymentOperationId operationId,
        DeviceId deviceId,
        DeviceDeploymentState state,
        DateTimeOffset updatedAtUtc)
    {
        if (!Enum.IsDefined(state))
        {
            throw new DomainInvariantException($"Unknown device deployment state '{state}'.");
        }

        return new DeviceDeployment(operationId, deviceId, state, updatedAtUtc.ToUniversalTime());
    }

    public void EnsureTransition(DeviceDeploymentState next, DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.TerminalImmutable}: terminal device deployments are immutable.");
        }

        if (!Allowed.Contains((State, next)))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.InvalidTransition}: device '{State}' → '{next}' is not allowed.");
        }

        State = next;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}
