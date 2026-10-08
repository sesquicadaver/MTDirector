using Mfc.Application.Jobs;
using Microsoft.Extensions.Options;

namespace Mfc.Controller.Jobs;

/// <summary>Executes one operational work item via Application job use cases (scoped DI).</summary>
public sealed class OperationalJobExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OperationalJobsOptions> _options;

    public OperationalJobExecutor(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OperationalJobsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public async Task ExecuteAsync(OperationalJobWorkItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        OperationalJobsOptions options = _options.CurrentValue;
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        switch (item.Kind)
        {
            case OperationalJobKind.OperationRecovery:
                {
                    RecoverNonterminalOperationsJobUseCase useCase =
                        sp.GetRequiredService<RecoverNonterminalOperationsJobUseCase>();
                    await useCase.ExecuteAsync(options.RecoveryBatchSize, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
            case OperationalJobKind.LockHeartbeat:
                {
                    HeartbeatDeploymentLocksJobUseCase deploymentLocks =
                        sp.GetRequiredService<HeartbeatDeploymentLocksJobUseCase>();
                    await deploymentLocks.ExecuteAsync(options.OwnerInstanceId, cancellationToken)
                        .ConfigureAwait(false);

                    // OWN-HB-01 / F01: onboarding Node leases need the same heartbeat as deployment.
                    HeartbeatOnboardingLocksJobUseCase onboardingLocks =
                        sp.GetRequiredService<HeartbeatOnboardingLocksJobUseCase>();
                    await onboardingLocks.ExecuteAsync(options.OwnerInstanceId, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
            case OperationalJobKind.ExpiredExceptionReconciliation:
                {
                    ReconcileExpiredExceptionBindingsJobUseCase exceptions =
                        sp.GetRequiredService<ReconcileExpiredExceptionBindingsJobUseCase>();
                    await exceptions.ExecuteAsync(
                            options.SystemActor,
                            options.ExpiredExceptionBatchSize,
                            cancellationToken)
                        .ConfigureAwait(false);

                    // AUDIT-M7-01 / F13: incident deny-overlay TTL uses the same expiry tick.
                    ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase incidents =
                        sp.GetRequiredService<ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase>();
                    await incidents.ExecuteAsync(
                            options.SystemActor,
                            options.ExpiredExceptionBatchSize,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
            case OperationalJobKind.WatchdogResidueCleanup:
                {
                    if (item.DeviceId is null || item.CandidateNames.Count == 0)
                    {
                        break;
                    }

                    CleanupDisabledWatchdogResidueJobUseCase useCase =
                        sp.GetRequiredService<CleanupDisabledWatchdogResidueJobUseCase>();
                    await useCase.ExecuteAsync(item.DeviceId.Value, item.CandidateNames, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
            case OperationalJobKind.DriftCapture:
                {
                    PollManagedDriftJobUseCase useCase = sp.GetRequiredService<PollManagedDriftJobUseCase>();
                    await useCase.ExecuteAsync(options.SystemActor, options.DriftBatchSize, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
            default:
                throw new InvalidOperationException($"Unknown operational job kind '{item.Kind}'.");
        }
    }
}

