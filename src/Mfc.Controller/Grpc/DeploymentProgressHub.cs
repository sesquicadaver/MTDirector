using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.Controller.Grpc;

/// <summary>
/// In-memory fan-out for WatchDeployment (M4-12).
/// Replay includes events after an earlier terminal (Committed → rollback) so a second Watch sees rollback progress
/// while the operation remains retained. Prunes after terminal + last reader leaves (AUDIT-INT-01 §19).
/// </summary>
public sealed class DeploymentProgressHub
{
    private readonly ConcurrentDictionary<Guid, OperationStream> _operations = new();

    public void Ensure(Guid operationId)
        => _operations.GetOrAdd(operationId, id => new OperationStream(id, ownerActor: null, TryPrune));

    /// <summary>Ensures the stream exists and binds <paramref name="ownerActor"/> when absent (WATCH-OWN-01).</summary>
    public void Ensure(Guid operationId, string ownerActor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerActor);
        string owner = ownerActor.Trim();
        _operations.AddOrUpdate(
            operationId,
            id => new OperationStream(id, owner, TryPrune),
            (_, existing) =>
            {
                existing.BindOwnerIfAbsent(owner);
                return existing;
            });
    }

    /// <summary>True when the operation is still retained in the hub (tests / diagnostics).</summary>
    public bool Contains(Guid operationId) => _operations.ContainsKey(operationId);

    /// <summary>Resolves the Start owner actor for Watch ACL (WATCH-OWN-01).</summary>
    public bool TryGetOwnerActor(Guid operationId, out string ownerActor)
    {
        ownerActor = string.Empty;
        if (!_operations.TryGetValue(operationId, out OperationStream? stream)
            || string.IsNullOrWhiteSpace(stream.OwnerActor))
        {
            return false;
        }

        ownerActor = stream.OwnerActor;
        return true;
    }

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
        }
    }

    private void TryPrune(Guid operationId) => _operations.TryRemove(operationId, out _);

    private sealed class OperationStream
    {
        private readonly object _gate = new();
        private readonly List<DeploymentProgress> _history = [];
        private readonly List<Channel<DeploymentProgress>> _subscribers = [];
        private readonly Action<Guid> _onIdleTerminal;
        private int _activeReaders;
        private bool _terminal;

        public OperationStream(Guid operationId, string? ownerActor, Action<Guid> onIdleTerminal)
        {
            OperationId = operationId;
            OwnerActor = string.IsNullOrWhiteSpace(ownerActor) ? null : ownerActor.Trim();
            _onIdleTerminal = onIdleTerminal;
        }

        public Guid OperationId { get; }

        public string? OwnerActor { get; private set; }

        public void BindOwnerIfAbsent(string ownerActor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerActor);
            if (string.IsNullOrWhiteSpace(OwnerActor))
            {
                OwnerActor = ownerActor.Trim();
            }
        }

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
                _activeReaders++;
                replay = [.. _history];
                alreadyTerminal = _terminal;
                if (!alreadyTerminal)
                {
                    _subscribers.Add(channel);
                }
            }

            try
            {
                foreach (DeploymentProgress item in replay)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return item;
                }

                if (alreadyTerminal)
                {
                    yield break;
                }

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
                bool prune;
                lock (_gate)
                {
                    _subscribers.Remove(channel);
                    _activeReaders--;
                    prune = _terminal && _activeReaders == 0 && _subscribers.Count == 0;
                }

                channel.Writer.TryComplete();
                if (prune)
                {
                    _onIdleTerminal(OperationId);
                }
            }
        }
    }
}
