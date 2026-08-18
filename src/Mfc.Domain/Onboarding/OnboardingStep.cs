using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Write-ahead onboarding journal step (Onboarding Spec §54).</summary>
public sealed class OnboardingStep
{
    private OnboardingStep(
        OnboardingStepId id,
        OnboardingOperationId operationId,
        DeviceId deviceId,
        int sequence,
        OnboardingStepKind kind,
        Hash256 expectedBeforeHash,
        Hash256 desiredAfterHash,
        OnboardingStepState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        OperationId = operationId;
        DeviceId = deviceId;
        Sequence = sequence;
        Kind = kind;
        ExpectedBeforeHash = expectedBeforeHash;
        DesiredAfterHash = desiredAfterHash;
        State = state;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public OnboardingStepId Id { get; }

    public OnboardingOperationId OperationId { get; }

    public DeviceId DeviceId { get; }

    public int Sequence { get; }

    public OnboardingStepKind Kind { get; }

    public Hash256 ExpectedBeforeHash { get; }

    public Hash256 DesiredAfterHash { get; }

    public OnboardingStepState State { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsTerminal
        => State is OnboardingStepState.Verified or OnboardingStepState.Failed;

    /// <summary>Records intent before any RouterOS effect (write-ahead).</summary>
    public static OnboardingStep Create(
        OnboardingOperationId operationId,
        DeviceId deviceId,
        int sequence,
        OnboardingStepKind kind,
        Hash256 expectedBeforeHash,
        Hash256 desiredAfterHash,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(expectedBeforeHash);
        ArgumentNullException.ThrowIfNull(desiredAfterHash);
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException($"Unknown onboarding step kind '{kind}'.");
        }

        if (sequence < 1)
        {
            throw new DomainInvariantException("step sequence must be >= 1.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        return new OnboardingStep(
            OnboardingStepId.New(),
            operationId,
            deviceId,
            sequence,
            kind,
            expectedBeforeHash,
            desiredAfterHash,
            OnboardingStepState.IntentRecorded,
            now,
            now);
    }

    public static OnboardingStep Reconstitute(
        OnboardingStepId id,
        OnboardingOperationId operationId,
        DeviceId deviceId,
        int sequence,
        OnboardingStepKind kind,
        Hash256 expectedBeforeHash,
        Hash256 desiredAfterHash,
        OnboardingStepState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(expectedBeforeHash);
        ArgumentNullException.ThrowIfNull(desiredAfterHash);
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException($"Unknown onboarding step kind '{kind}'.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new DomainInvariantException($"Unknown onboarding step state '{state}'.");
        }

        if (sequence < 1)
        {
            throw new DomainInvariantException("step sequence must be >= 1.");
        }

        return new OnboardingStep(
            id,
            operationId,
            deviceId,
            sequence,
            kind,
            expectedBeforeHash,
            desiredAfterHash,
            state,
            createdAtUtc.ToUniversalTime(),
            updatedAtUtc.ToUniversalTime());
    }

    public void RecordEffectSent(DateTimeOffset nowUtc)
        => Transition(OnboardingStepState.IntentRecorded, OnboardingStepState.EffectSent, nowUtc);

    public void MarkVerified(DateTimeOffset nowUtc)
    {
        if (State == OnboardingStepState.IntentRecorded)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.StepInvalidTransition}: VERIFIED requires EFFECT_SENT.");
        }

        Transition(OnboardingStepState.EffectSent, OnboardingStepState.Verified, nowUtc);
    }

    public void MarkFailed(DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.TerminalImmutable}: terminal steps are immutable.");
        }

        if (State is not (OnboardingStepState.IntentRecorded or OnboardingStepState.EffectSent))
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.StepInvalidTransition}: '{State}' → FAILED is not allowed.");
        }

        State = OnboardingStepState.Failed;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    private void Transition(OnboardingStepState expectedFrom, OnboardingStepState next, DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.TerminalImmutable}: terminal steps are immutable.");
        }

        if (State != expectedFrom)
        {
            throw new DomainInvariantException(
                $"{OnboardingCodes.StepInvalidTransition}: expected '{expectedFrom}', was '{State}'.");
        }

        State = next;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}
