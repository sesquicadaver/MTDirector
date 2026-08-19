namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Per-device deployment state (Safe Deployment Spec §14 / M4-01).</summary>
public sealed class DeploymentDeviceStateEntity
{
    public const short CommittedState = 9;

    public const short RolledBackState = 11;

    public const short RecoveryRequiredState = 12;

    public Guid OperationId { get; set; }

    public Guid DeviceId { get; set; }

    public short State { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static bool IsTerminal(short state)
        => state is CommittedState or RolledBackState or RecoveryRequiredState;
}
