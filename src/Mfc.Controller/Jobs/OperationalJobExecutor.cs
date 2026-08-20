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
                    HeartbeatDeploymentLocksJobUseCase useCase =
                        sp.GetRequiredService<HeartbeatDeploymentLocksJobUseCase>();
                    await useCase.ExecuteAsync(options.OwnerInstanceId, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
            case OperationalJobKind.ExpiredExceptionReconciliation:
                {
                    ReconcileExpiredExceptionBindingsJobUseCase useCase =
                        sp.GetRequiredService<ReconcileExpiredExceptionBindingsJobUseCase>();
                    await useCase.ExecuteAsync(
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

