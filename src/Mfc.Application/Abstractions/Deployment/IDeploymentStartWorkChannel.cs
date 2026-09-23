using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Domain.Policy;

namespace Mfc.Application.Abstractions.Deployment;

/// <summary>
/// Accepted Start work for Controller-side execution independent of the unary RPC (AUDIT-RPC-01).
/// </summary>
public sealed class DeploymentStartWorkItem
{
    public required Guid OperationId { get; init; }

    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required byte[] RequestHash { get; init; }

    public required IReadOnlyList<PacketPathPairFact> PacketPathPairs { get; init; }

    public required string OwnerInstanceId { get; init; }

    /// <summary>Filled by <see cref="StartDeploymentUseCase.ContinueAcceptedAsync"/> when effects finish.</summary>
    public TaskCompletionSource<ApplicationResult<DeploymentOperationSummaryView>> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Queues durable-accepted deployment Starts for background execution (AUDIT-RPC-01).
/// </summary>
public interface IDeploymentStartWorkChannel
{
    /// <summary>
    /// When true, <see cref="EnqueueAsync"/> awaits Continue to completion (unit-test harness).
    /// When false, Start returns after Accept and a hosted worker runs Continue.
    /// </summary>
    bool RunsSynchronously { get; }

    ValueTask EnqueueAsync(DeploymentStartWorkItem item, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs Continue inline on Enqueue so Application unit tests keep observing terminal Start results.
/// </summary>
public sealed class ImmediateDeploymentStartWorkChannel : IDeploymentStartWorkChannel
{
    public bool RunsSynchronously => true;

    /// <summary>Bound to <see cref="StartDeploymentUseCase.ContinueAcceptedAsync"/> after construction.</summary>
    public Func<DeploymentStartWorkItem, CancellationToken, Task>? Handler { get; set; }

    public ValueTask EnqueueAsync(DeploymentStartWorkItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        Func<DeploymentStartWorkItem, CancellationToken, Task> handler = Handler
            ?? throw new InvalidOperationException("ImmediateDeploymentStartWorkChannel.Handler is not bound.");
        return new ValueTask(handler(item, cancellationToken));
    }
}
