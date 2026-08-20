using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Deployment;

/// <summary>
/// Plans production rollback watchdog resources (M4-05).
/// Execution is <see cref="IDeploymentWatchdogPort"/>; this use case never talks to RouterOS.
/// </summary>
public static class PlanDeploymentWatchdogUseCase
{
    public static DeploymentWatchdogPlanResult PlanWatchdog(
        DeploymentOperationId deploymentId,
        DeviceDeploymentPlan devicePlan,
        DeploymentSystemNameFacts names)
        => DeploymentWatchdogPlanner.PlanWatchdog(deploymentId, devicePlan, names);

    public static DeploymentWatchdogPlanResult EnsureAllDevicesArmed(
        IReadOnlyList<DeviceId> memberDeviceIds,
        IReadOnlySet<DeviceId> armedDeviceIds)
        => DeploymentWatchdogPlanner.EnsureAllDevicesArmed(memberDeviceIds, armedDeviceIds);
}

/// <summary>
/// Restricted production watchdog writer. Must use allowlisted deployment paths,
/// verify script source hash, and never expose a free-form command method.
/// </summary>
public interface IDeploymentWatchdogPort
{
    Task<DeploymentWatchdogExecutionResult> ArmWatchdogAsync(
        DeploymentWatchdogBundle bundle,
        DateTimeOffset routerClock,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default);

    Task<DeploymentWatchdogExecutionResult> DisarmWatchdogAsync(
        DeploymentWatchdogBundle bundle,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default);

    Task<DeploymentWatchdogExecutionResult> CleanupWatchdogAsync(
        DeploymentOperationId deploymentId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of production watchdog arm/disarm/cleanup plus read-back evidence.</summary>
public sealed class DeploymentWatchdogExecutionResult
{
    public required bool Succeeded { get; init; }

    public required string Code { get; init; }

    public required IReadOnlyList<string> Paths { get; init; }

    public Hash256? ObservedSourceHash { get; init; }

    public string? Error { get; init; }
}
