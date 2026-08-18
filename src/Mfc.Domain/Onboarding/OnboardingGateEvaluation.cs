namespace Mfc.Domain.Onboarding;

/// <summary>Pure-function result of <see cref="OnboardingOperationGate"/>.</summary>
public sealed class OnboardingGateEvaluation
{
    public required bool Allowed { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Alias of <see cref="ErrorMessage"/> for gate/operation call sites.</summary>
    public string? Message => ErrorMessage;

    public static OnboardingGateEvaluation Ok() => new() { Allowed = true };

    /// <summary>Alias of <see cref="Ok"/>.</summary>
    public static OnboardingGateEvaluation Allow() => Ok();

    public static OnboardingGateEvaluation Reject(string code, string message)
        => new()
        {
            Allowed = false,
            ErrorCode = code,
            ErrorMessage = message,
        };
}
