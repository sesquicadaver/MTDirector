using Mfc.Domain.Deployment;

namespace Mfc.Application.Abstractions.Deployment;

/// <summary>
/// Live deployment phase notifications during RouterOS effects (AUDIT-RPC-01).
/// Distinct from post-hoc timeline replay: reported as the operation state advances.
/// </summary>
public interface IDeploymentPhaseReporter
{
    void Report(DeploymentOperationState state, string? errorCode = null, string? timelineEntry = null);
}

/// <summary>Adapts <see cref="IDeploymentProgressSink"/> for a single operation.</summary>
public sealed class SinkDeploymentPhaseReporter : IDeploymentPhaseReporter
{
    private readonly IDeploymentProgressSink _sink;
    private readonly Guid _operationId;

    public SinkDeploymentPhaseReporter(IDeploymentProgressSink sink, Guid operationId)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _operationId = operationId;
    }

    public void Report(DeploymentOperationState state, string? errorCode = null, string? timelineEntry = null)
        => _sink.Publish(_operationId, state, errorCode, timelineEntry);
}
