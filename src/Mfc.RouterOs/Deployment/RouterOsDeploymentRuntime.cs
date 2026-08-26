using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Production <see cref="IDeploymentRuntime"/> over closed deployment writers (P2-08 / #294).
/// DI registration is gated by write-path enablement in P2-10.
/// </summary>
public sealed class RouterOsDeploymentRuntime : IDeploymentRuntime
{
    private readonly IRouterOsDeploymentSessionFactory _sessions;
    private readonly IDeploymentArtifactMaterializer _artifacts;

    public RouterOsDeploymentRuntime(
        IRouterOsDeploymentSessionFactory sessions,
        IDeploymentArtifactMaterializer artifacts)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(artifacts);
        _sessions = sessions;
        _artifacts = artifacts;
    }

    public async Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        IReadOnlyList<PacketPathPairFact> packetPathPairs,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(packetPathPairs);
        cancellationToken.ThrowIfCancellationRequested();

        await using RouterOsDeploymentScopedSessions scope = await _sessions
            .OpenAsync(node, plan, operation.Id, cancellationToken)
            .ConfigureAwait(false);

        if (node.DeclaredKind == NodeKind.Vrrp)
        {
            List<IVrrpMemberDeploymentRuntime> members = [];
            foreach (IDeploymentLiveDeviceSession live in scope.Sessions)
            {
                if (live is not RouterOsDeploymentDeviceSession session)
                {
                    throw new InvalidOperationException("VRRP deployment requires live RouterOS device sessions.");
                }

                DeviceDeploymentPlan memberPlan = plan.DevicePlans.Single(p => p.DeviceId == session.DeviceId);
                DeploymentStagingArtifacts material = await _artifacts
                    .LoadAsync(memberPlan, cancellationToken)
                    .ConfigureAwait(false);
                members.Add(new RouterOsVrrpMemberDeploymentRuntime(session, memberPlan, material, nowUtc));
            }

            VrrpDeploymentResult result = await ExecuteVrrpDeploymentUseCase.ExecuteAsync(
                node,
                plan,
                operation,
                members,
                [],
                packetPathPairs,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
            return new DeploymentWorkflowExecutionResult
            {
                Succeeded = result.Succeeded,
                State = result.State,
                ErrorCode = result.ErrorCode,
                Timeline = result.Timeline,
                ActivationStarted = result.Succeeded && result.State == DeploymentOperationState.Committed,
            };
        }

        IDeploymentLiveDeviceSession device = scope.Sessions.Single();
        DeviceDeploymentPlan devicePlan = plan.DevicePlans.Single();
        DeploymentStagingArtifacts staging = await _artifacts.LoadAsync(devicePlan, cancellationToken)
            .ConfigureAwait(false);
        DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, devicePlan.DeviceId, nowUtc);
        StandaloneDeploymentResult standalone = await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
            node,
            plan,
            operation,
            deviceState,
            device,
            [],
            packetPathPairs,
            staging.AddressLists,
            staging.Chains,
            devicePlan.NewArtifactHash,
            nowUtc,
            nowUtc,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new DeploymentWorkflowExecutionResult
        {
            Succeeded = standalone.Succeeded,
            State = standalone.State,
            ErrorCode = standalone.ErrorCode,
            Timeline = standalone.Timeline,
            ActivationStarted = standalone.WatchdogArmedBeforeActivation
                || ActivationStarted(standalone.State),
        };
    }

    public async Task<DeploymentWorkflowRollbackResult> RollbackAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        await using RouterOsDeploymentScopedSessions scope = await _sessions
            .OpenAsync(node, plan, operation.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<IDeploymentRollbackDeviceRuntime> devices = scope.Sessions
            .Cast<IDeploymentRollbackDeviceRuntime>()
            .ToArray();
        DeploymentRollbackResult rolled = await ExecuteDeploymentRollbackUseCase.ExecuteAsync(
            plan,
            operation,
            devices,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return new DeploymentWorkflowRollbackResult
        {
            Succeeded = rolled.Succeeded,
            State = rolled.State,
            ErrorCode = rolled.ErrorCode,
            Timeline = rolled.Timeline,
        };
    }

    public async Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
        Node node,
        DeploymentPlan plan,
        DeploymentOperation operation,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        await using RouterOsDeploymentScopedSessions scope = await _sessions
            .OpenAsync(node, plan, operation.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<IDeploymentRollbackDeviceRuntime> devices = scope.Sessions
            .Cast<IDeploymentRollbackDeviceRuntime>()
            .ToArray();
        bool activationStarted = ActivationStarted(operation.State)
            || operation.State is DeploymentOperationState.Committed
                or DeploymentOperationState.RolledBack
                or DeploymentOperationState.RecoveryRequired;
        DeploymentRecoveryResult recovered = await RecoverDeploymentUseCase.ExecuteAsync(
            plan,
            operation,
            devices,
            activationStarted,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
        return new DeploymentWorkflowRecoveryResult
        {
            Action = recovered.Action,
            State = recovered.State,
            ErrorCode = recovered.ErrorCode,
            Timeline = recovered.Timeline,
        };
    }

    private static bool ActivationStarted(DeploymentOperationState state)
        => state is DeploymentOperationState.Activating
            or DeploymentOperationState.Verifying
            or DeploymentOperationState.DisarmingWatchdog
            or DeploymentOperationState.RollbackPending
            or DeploymentOperationState.RollingBack;
}
