using Mfc.Domain.Deployment;
using Mfc.Domain.Policy;

namespace Mfc.Application.Abstractions.Deployment;

/// <summary>
/// Live deployment phase notifications for Watch (AUDIT-RPC-01).
/// Controller adapts this to <c>DeploymentProgressHub</c>; unit tests use a no-op sink.
/// </summary>
public interface IDeploymentProgressSink
{
    void Ensure(Guid operationId, string ownerActor);

    void Publish(
        Guid operationId,
        DeploymentOperationState state,
        string? errorCode = null,
        string? timelineEntry = null);
}

/// <summary>No-op progress sink for Application unit tests without a Watch hub.</summary>
public sealed class NullDeploymentProgressSink : IDeploymentProgressSink
{
    public static NullDeploymentProgressSink Instance { get; } = new();

    public void Ensure(Guid operationId, string ownerActor)
    {
        _ = operationId;
        _ = ownerActor;
    }

    public void Publish(
        Guid operationId,
        DeploymentOperationState state,
        string? errorCode = null,
        string? timelineEntry = null)
    {
        _ = operationId;
        _ = state;
        _ = errorCode;
        _ = timelineEntry;
    }
}
