namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>Write-ahead onboarding journal step (Onboarding Spec §54 / M5-01).</summary>
public sealed class OnboardingStepEntity
{
    public const short VerifiedState = 2;

    public const short FailedState = 3;

    public Guid Id { get; set; }

    public Guid OperationId { get; set; }

    public Guid DeviceId { get; set; }

    public long Sequence { get; set; }

    public short Kind { get; set; }

    public required byte[] ExpectedBeforeHash { get; set; }

    public required byte[] DesiredAfterHash { get; set; }

    public short State { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static bool IsTerminal(short state) => state is VerifiedState or FailedState;
}
