using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Contracts-only OnboardingService client (ADR 0005 / M5-09).</summary>
public interface IOnboardingServiceClient
{
    Task<OnboardingPrerequisiteReport> ValidatePrerequisitesAsync(
        Guid nodeId,
        IReadOnlyList<OnboardingDevicePrerequisiteFacts> devices,
        CancellationToken cancellationToken = default);

    Task<OnboardingPlanSummary> CreatePlanAsync(
        Guid nodeId,
        Sha256 membershipHash,
        Sha256 topologyHash,
        IReadOnlyList<OnboardingDevicePlanInput> devices,
        CancellationToken cancellationToken = default);

    Task<OnboardingOperationSummary> StartAsync(
        Guid planId,
        Sha256 planHash,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<OnboardingProgress> WatchAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<OnboardingOperationSummary> RollbackAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<OnboardingRecoveryStatus> GetRecoveryStatusAsync(
        Guid nodeId,
        Guid? operationId = null,
        CancellationToken cancellationToken = default);
}
