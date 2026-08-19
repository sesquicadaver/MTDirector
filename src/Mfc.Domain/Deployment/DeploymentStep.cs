using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>Write-ahead deployment journal step (Safe Deployment Spec §16).</summary>
public sealed class DeploymentStep
{
    private DeploymentStep(
        DeploymentStepId id,
        DeploymentOperationId operationId,
        DeviceId deviceId,
        int sequence,
        DeploymentStepKind kind,
        Hash256 expectedBeforeHash,
        Hash256 desiredAfterHash,
        DeploymentStepState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        string? sanitizedError)
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
        SanitizedError = sanitizedError;
    }

    public DeploymentStepId Id { get; }

    public DeploymentOperationId OperationId { get; }

    public DeviceId DeviceId { get; }

    public int Sequence { get; }

    public DeploymentStepKind Kind { get; }

    public Hash256 ExpectedBeforeHash { get; }

    public Hash256 DesiredAfterHash { get; }

    public DeploymentStepState State { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? SanitizedError { get; private set; }

    public bool IsTerminal
        => State is DeploymentStepState.Verified or DeploymentStepState.Failed;

    public static DeploymentStep Create(
        DeploymentOperationId operationId,
        DeviceId deviceId,
        int sequence,
        DeploymentStepKind kind,
        Hash256 expectedBeforeHash,
        Hash256 desiredAfterHash,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(expectedBeforeHash);
        ArgumentNullException.ThrowIfNull(desiredAfterHash);
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException($"Unknown deployment step kind '{kind}'.");
        }

        if (sequence < 1)
        {
            throw new DomainInvariantException("step sequence must be >= 1.");
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        return new DeploymentStep(
            DeploymentStepId.New(),
            operationId,
            deviceId,
            sequence,
            kind,
            expectedBeforeHash,
            desiredAfterHash,
            DeploymentStepState.IntentRecorded,
            now,
            now,
            sanitizedError: null);
    }

    public static DeploymentStep Reconstitute(
        DeploymentStepId id,
        DeploymentOperationId operationId,
        DeviceId deviceId,
        int sequence,
        DeploymentStepKind kind,
        Hash256 expectedBeforeHash,
        Hash256 desiredAfterHash,
        DeploymentStepState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        string? sanitizedError)
    {
        ArgumentNullException.ThrowIfNull(expectedBeforeHash);
        ArgumentNullException.ThrowIfNull(desiredAfterHash);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(state) || sequence < 1)
        {
            throw new DomainInvariantException("Invalid reconstituted deployment step.");
        }

        return new DeploymentStep(
            id,
            operationId,
            deviceId,
            sequence,
            kind,
            expectedBeforeHash,
            desiredAfterHash,
            state,
            createdAtUtc.ToUniversalTime(),
            updatedAtUtc.ToUniversalTime(),
            sanitizedError);
    }

    public void RecordEffectSent(DateTimeOffset nowUtc)
        => Transition(DeploymentStepState.IntentRecorded, DeploymentStepState.EffectSent, nowUtc);

    public void MarkVerified(DateTimeOffset nowUtc)
    {
        if (State == DeploymentStepState.IntentRecorded)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.StepInvalidTransition}: VERIFIED requires EFFECT_SENT.");
        }

        Transition(DeploymentStepState.EffectSent, DeploymentStepState.Verified, nowUtc);
    }

    public void MarkFailed(DateTimeOffset nowUtc, string? sanitizedError = null)
    {
        if (IsTerminal)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.TerminalImmutable}: terminal steps are immutable.");
        }

        if (State is not (DeploymentStepState.IntentRecorded or DeploymentStepState.EffectSent))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.StepInvalidTransition}: '{State}' → FAILED is not allowed.");
        }

        State = DeploymentStepState.Failed;
        SanitizedError = string.IsNullOrWhiteSpace(sanitizedError) ? null : sanitizedError.Trim();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    private void Transition(DeploymentStepState expectedFrom, DeploymentStepState next, DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.TerminalImmutable}: terminal steps are immutable.");
        }

        if (State != expectedFrom)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.StepInvalidTransition}: expected '{expectedFrom}', was '{State}'.");
        }

        State = next;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}
