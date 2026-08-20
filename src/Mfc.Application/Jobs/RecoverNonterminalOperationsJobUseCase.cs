using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Onboarding;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Jobs;

/// <summary>Outcome of one recovery attempt from the bounded recovery job.</summary>
public sealed class OperationRecoveryJobItemResult
{
    public required Guid OperationId { get; init; }

    public required string Kind { get; init; }

    public required bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }
}

/// <summary>Batch result for nonterminal operation recovery after restart (M6-03).</summary>
public sealed class RecoverNonterminalOperationsJobResult
{
    public required IReadOnlyList<OperationRecoveryJobItemResult> Items { get; init; }
}

/// <summary>
/// Recovers nonterminal deployment/onboarding operations via existing runtimes (higher priority than drift).
/// </summary>
public sealed class RecoverNonterminalOperationsJobUseCase
{
    private readonly IDeploymentStore _deployments;
    private readonly IOnboardingStore _onboardings;
    private readonly INodeStore _nodes;
    private readonly IDeploymentRuntime _deploymentRuntime;
    private readonly IOnboardingRuntime _onboardingRuntime;
    private readonly IClock _clock;

    public RecoverNonterminalOperationsJobUseCase(
        IDeploymentStore deployments,
        IOnboardingStore onboardings,
        INodeStore nodes,
        IDeploymentRuntime deploymentRuntime,
        IOnboardingRuntime onboardingRuntime,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentNullException.ThrowIfNull(onboardings);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(deploymentRuntime);
        ArgumentNullException.ThrowIfNull(onboardingRuntime);
        ArgumentNullException.ThrowIfNull(clock);
        _deployments = deployments;
        _onboardings = onboardings;
        _nodes = nodes;
        _deploymentRuntime = deploymentRuntime;
        _onboardingRuntime = onboardingRuntime;
        _clock = clock;
    }

    public async Task<ApplicationResult<RecoverNonterminalOperationsJobResult>> ExecuteAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize < 1)
        {
            return ApplicationResults.Fail(ApplicationError.Validation("batchSize must be >= 1."));
        }

        List<OperationRecoveryJobItemResult> items = [];
        DateTimeOffset now = _clock.UtcNow;

        IReadOnlyList<DeploymentOperation> deployments = await _deployments
            .ListNonterminalAsync(batchSize, cancellationToken)
            .ConfigureAwait(false);
        foreach (DeploymentOperation operation in deployments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(await RecoverDeploymentAsync(operation, now, cancellationToken).ConfigureAwait(false));
        }

        int remaining = Math.Max(0, batchSize - items.Count);
        if (remaining > 0)
        {
            IReadOnlyList<OnboardingOperation> onboardings = await _onboardings
                .ListNonterminalAsync(remaining, cancellationToken)
                .ConfigureAwait(false);
            foreach (OnboardingOperation operation in onboardings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add(await RecoverOnboardingAsync(operation, now, cancellationToken).ConfigureAwait(false));
            }
        }

        return ApplicationResults.Ok(new RecoverNonterminalOperationsJobResult { Items = items });
    }

    private async Task<OperationRecoveryJobItemResult> RecoverDeploymentAsync(
        DeploymentOperation operation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            Node? node = await _nodes.GetAsync(operation.NodeId, cancellationToken).ConfigureAwait(false);
            DeploymentPlan? plan = await _deployments.GetPlanAsync(operation.PlanId, cancellationToken)
                .ConfigureAwait(false);
            if (node is null || plan is null)
            {
                return new OperationRecoveryJobItemResult
                {
                    OperationId = operation.Id.Value,
                    Kind = "deployment",
                    Succeeded = false,
                    ErrorCode = "not_found",
                };
            }

            DeploymentWorkflowRecoveryResult recovered = await _deploymentRuntime
                .RecoverAsync(node, plan, operation, now, cancellationToken)
                .ConfigureAwait(false);
            await _deployments.SaveOperationAsync(operation, cancellationToken).ConfigureAwait(false);
            bool succeeded = recovered.Action is not DeploymentRecoveryAction.RecoveryRequired
                             && recovered.ErrorCode is null;
            return new OperationRecoveryJobItemResult
            {
                OperationId = operation.Id.Value,
                Kind = "deployment",
                Succeeded = succeeded,
                ErrorCode = recovered.ErrorCode,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new OperationRecoveryJobItemResult
            {
                OperationId = operation.Id.Value,
                Kind = "deployment",
                Succeeded = false,
                ErrorCode = ex.GetType().Name,
            };
        }
    }

    private async Task<OperationRecoveryJobItemResult> RecoverOnboardingAsync(
        OnboardingOperation operation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            Node? node = await _nodes.GetAsync(operation.NodeId, cancellationToken).ConfigureAwait(false);
            OnboardingPlan? plan = await _onboardings.GetPlanAsync(operation.PlanId, cancellationToken)
                .ConfigureAwait(false);
            if (node is null || plan is null)
            {
                return new OperationRecoveryJobItemResult
                {
                    OperationId = operation.Id.Value,
                    Kind = "onboarding",
                    Succeeded = false,
                    ErrorCode = "not_found",
                };
            }

            OnboardingRecoveryResult recovered = await _onboardingRuntime
                .RecoverAsync(node, plan, operation, now, cancellationToken)
                .ConfigureAwait(false);
            await _onboardings.SaveOperationAsync(operation, cancellationToken).ConfigureAwait(false);
            return new OperationRecoveryJobItemResult
            {
                OperationId = operation.Id.Value,
                Kind = "onboarding",
                Succeeded = recovered.ErrorCode is null,
                ErrorCode = recovered.ErrorCode,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new OperationRecoveryJobItemResult
            {
                OperationId = operation.Id.Value,
                Kind = "onboarding",
                Succeeded = false,
                ErrorCode = ex.GetType().Name,
            };
        }
    }
}
