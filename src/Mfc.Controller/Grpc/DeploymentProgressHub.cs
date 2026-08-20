using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.Controller.Grpc;

/// <summary>In-memory fan-out for WatchDeployment (M4-12).</summary>
public sealed class DeploymentProgressHub
{
    private readonly ConcurrentDictionary<Guid, OperationStream> _operations = new();

    public void Ensure(Guid operationId)
        => _operations.GetOrAdd(operationId, id => new OperationStream(id));

    public void Publish(Guid operationId, DomainState state, string? errorCode = null, string? timelineEntry = null)
    {
        Ensure(operationId);
        if (!_operations.TryGetValue(operationId, out OperationStream? stream))
        {
            return;
        }

        DeploymentProgress progress = new()
        {
            OperationId = ProtoUuid.FromGuid(operationId),
            State = DeploymentProtoMapper.ToProto(state),
            OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        };
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            progress.ErrorCode = errorCode;
        }

        if (!string.IsNullOrWhiteSpace(timelineEntry))
        {
            progress.TimelineEntry = timelineEntry;
        }

        stream.Publish(progress);
    }

    public async IAsyncEnumerable<DeploymentProgress> WatchAsync(
        Guid operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_operations.TryGetValue(operationId, out OperationStream? stream))
        {
            yield break;
        }

        await foreach (DeploymentProgress progress in stream.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return progress;
            if (DeploymentProtoMapper.IsTerminal(progress.State))
            {
                yield break;
            }
        }
    }

    private sealed class OperationStream
    {
        private readonly object _gate = new();
        private readonly List<DeploymentProgress> _history = [];
        private readonly List<Channel<DeploymentProgress>> _subscribers = [];
        private bool _terminal;

        public OperationStream(Guid operationId) => OperationId = operationId;

        public Guid OperationId { get; }

        public void Publish(DeploymentProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            List<Channel<DeploymentProgress>> subscribers;
            lock (_gate)
            {
                _history.Add(progress);
                if (DeploymentProtoMapper.IsTerminal(progress.State))
                {
                    _terminal = true;
                }

                subscribers = [.. _subscribers];
            }

            foreach (Channel<DeploymentProgress> channel in subscribers)
            {
                channel.Writer.TryWrite(progress);
            }

            if (_terminal)
            {
                foreach (Channel<DeploymentProgress> channel in subscribers)
                {
                    channel.Writer.TryComplete();
                }
            }
        }

        public async IAsyncEnumerable<DeploymentProgress> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Channel<DeploymentProgress> channel = Channel.CreateUnbounded<DeploymentProgress>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false,
                });

            DeploymentProgress[] replay;
            bool alreadyTerminal;
            lock (_gate)
            {
                replay = [.. _history];
                alreadyTerminal = _terminal;
                if (!alreadyTerminal)
                {
                    _subscribers.Add(channel);
                }
            }

            foreach (DeploymentProgress item in replay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                if (DeploymentProtoMapper.IsTerminal(item.State))
                {
                    yield break;
                }
            }

            if (alreadyTerminal)
            {
                yield break;
            }

            try
            {
                await foreach (DeploymentProgress item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return item;
                    if (DeploymentProtoMapper.IsTerminal(item.State))
                    {
                        yield break;
                    }
                }
            }
            finally
            {
                lock (_gate)
                {
                    _subscribers.Remove(channel);
                }

                channel.Writer.TryComplete();
            }
        }
    }
}
