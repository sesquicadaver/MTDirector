using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;
using DomainState = Mfc.Domain.Onboarding.OnboardingOperationState;

namespace Mfc.Controller.Grpc;

/// <summary>
/// In-memory fan-out for WatchOnboarding (M5-09).
/// Replay includes events after an earlier terminal (Committed → rollback) so a second Watch sees rollback progress
/// (W6-04 / CONT-01 parity) while retained. Prunes after terminal + last reader leaves (AUDIT-INT-01 §19).
/// </summary>
public sealed class OnboardingProgressHub
{
    private readonly ConcurrentDictionary<Guid, OperationStream> _operations = new();

    public void Ensure(Guid operationId)
        => _operations.GetOrAdd(operationId, id => new OperationStream(id, TryPrune));

    /// <summary>True when the operation is still retained in the hub (tests / diagnostics).</summary>
    public bool Contains(Guid operationId) => _operations.ContainsKey(operationId);

    public void Publish(Guid operationId, DomainState state, string? errorCode = null, string? timelineEntry = null)
    {
        Ensure(operationId);
        if (!_operations.TryGetValue(operationId, out OperationStream? stream))
        {
            return;
        }

        OnboardingProgress progress = new()
        {
            OperationId = ProtoUuid.FromGuid(operationId),
            State = OnboardingProtoMapper.ToProto(state),
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

    public async IAsyncEnumerable<OnboardingProgress> WatchAsync(
        Guid operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_operations.TryGetValue(operationId, out OperationStream? stream))
        {
            yield break;
        }

        await foreach (OnboardingProgress progress in stream.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return progress;
        }
    }

    private void TryPrune(Guid operationId) => _operations.TryRemove(operationId, out _);

    private sealed class OperationStream
    {
        private readonly object _gate = new();
        private readonly List<OnboardingProgress> _history = [];
        private readonly List<Channel<OnboardingProgress>> _subscribers = [];
        private readonly Action<Guid> _onIdleTerminal;
        private int _activeReaders;
        private bool _terminal;

        public OperationStream(Guid operationId, Action<Guid> onIdleTerminal)
        {
            OperationId = operationId;
            _onIdleTerminal = onIdleTerminal;
        }

        public Guid OperationId { get; }

        public void Publish(OnboardingProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            List<Channel<OnboardingProgress>> subscribers;
            lock (_gate)
            {
                _history.Add(progress);
                if (OnboardingProtoMapper.IsTerminal(progress.State))
                {
                    _terminal = true;
                }

                subscribers = [.. _subscribers];
            }

            foreach (Channel<OnboardingProgress> channel in subscribers)
            {
                channel.Writer.TryWrite(progress);
            }

            if (_terminal)
            {
                foreach (Channel<OnboardingProgress> channel in subscribers)
                {
                    channel.Writer.TryComplete();
                }
            }
        }

        public async IAsyncEnumerable<OnboardingProgress> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Channel<OnboardingProgress> channel = Channel.CreateUnbounded<OnboardingProgress>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false,
                });

            OnboardingProgress[] replay;
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
                foreach (OnboardingProgress item in replay)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return item;
                }

                if (alreadyTerminal)
                {
                    yield break;
                }

                await foreach (OnboardingProgress item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return item;
                    if (OnboardingProtoMapper.IsTerminal(item.State))
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
