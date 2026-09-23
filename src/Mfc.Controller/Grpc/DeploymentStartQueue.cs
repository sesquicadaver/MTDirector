using System.Threading.Channels;
using Mfc.Application.Abstractions.Deployment;
using Mfc.Domain.Deployment;

namespace Mfc.Controller.Grpc;

/// <summary>Adapts <see cref="DeploymentProgressHub"/> to Application progress sink (AUDIT-RPC-01).</summary>
public sealed class HubDeploymentProgressSink : IDeploymentProgressSink
{
    private readonly DeploymentProgressHub _hub;

    public HubDeploymentProgressSink(DeploymentProgressHub hub)
    {
        ArgumentNullException.ThrowIfNull(hub);
        _hub = hub;
    }

    public void Ensure(Guid operationId, string ownerActor) => _hub.Ensure(operationId, ownerActor);

    public void Publish(
        Guid operationId,
        DeploymentOperationState state,
        string? errorCode = null,
        string? timelineEntry = null)
        => _hub.Publish(operationId, state, errorCode, timelineEntry);
}

/// <summary>In-process queue for accepted deployment Starts (AUDIT-RPC-01).</summary>
public sealed class ChannelDeploymentStartWorkChannel : IDeploymentStartWorkChannel
{
    private readonly Channel<DeploymentStartWorkItem> _channel = Channel.CreateUnbounded<DeploymentStartWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    public bool RunsSynchronously => false;

    public ChannelReader<DeploymentStartWorkItem> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(DeploymentStartWorkItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_channel.Writer.TryWrite(item))
        {
            return new ValueTask(
                Task.FromException(new InvalidOperationException("Deployment start queue rejected the work item.")));
        }

        return ValueTask.CompletedTask;
    }
}
