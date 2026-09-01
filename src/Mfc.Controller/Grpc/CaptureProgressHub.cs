using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>
/// In-memory fan-out for CaptureProgress streams (M1-26 WatchCapture).
/// Buffers events so late WatchCapture subscribers still observe COMPLETED/FAILED.
/// </summary>
public sealed class CaptureProgressHub
{
    private readonly ConcurrentDictionary<Guid, OperationStream> _operations = new();

    /// <summary>Registers a new capture operation and returns its id.</summary>
    public Guid Begin(Guid deviceId)
    {
        Guid operationId = Guid.NewGuid();
        if (!_operations.TryAdd(operationId, new OperationStream(operationId, deviceId)))
        {
            throw new InvalidOperationException("Failed to register capture operation.");
        }

        return operationId;
    }

    /// <summary>
    /// Publishes a progress event to all watchers of <paramref name="operationId"/>.
    /// When <paramref name="deviceId"/> is set, it overrides the operation's default device
    /// (Node fan-out; W6-03).
    /// </summary>
    public void Publish(
        Guid operationId,
        CaptureStage stage,
        Guid? captureId = null,
        ErrorDetail? error = null,
        Guid? deviceId = null)
    {
        if (!_operations.TryGetValue(operationId, out OperationStream? stream))
        {
            return;
        }

        Guid progressDeviceId = deviceId ?? stream.DeviceId;
        CaptureProgress progress = new()
        {
            OperationId = ProtoUuid.FromGuid(operationId),
            DeviceId = ProtoUuid.FromGuid(progressDeviceId),
            Stage = stage,
            OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
        };
        if (captureId is Guid id)
        {
            progress.CaptureId = ProtoUuid.FromGuid(id);
        }

        if (error is not null)
        {
            progress.Error = error;
        }

        stream.Publish(progress);
    }

    /// <summary>Streams buffered + live progress until a terminal stage or cancellation.</summary>
    public async IAsyncEnumerable<CaptureProgress> WatchAsync(
        Guid operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_operations.TryGetValue(operationId, out OperationStream? stream))
        {
            yield break;
        }

        await foreach (CaptureProgress progress in stream.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return progress;
            if (IsTerminal(progress.Stage))
            {
                yield break;
            }
        }
    }

    private static bool IsTerminal(CaptureStage stage)
        => stage is CaptureStage.Completed or CaptureStage.Failed or CaptureStage.Canceled;

    private sealed class OperationStream
    {
        private readonly object _gate = new();
        private readonly List<CaptureProgress> _history = [];
        private readonly List<Channel<CaptureProgress>> _subscribers = [];
        private bool _terminal;

        public OperationStream(Guid operationId, Guid deviceId)
        {
            OperationId = operationId;
            DeviceId = deviceId;
        }

        public Guid OperationId { get; }

        public Guid DeviceId { get; }

        public void Publish(CaptureProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            List<Channel<CaptureProgress>> subscribers;
            lock (_gate)
            {
                _history.Add(progress);
                if (IsTerminal(progress.Stage))
                {
                    _terminal = true;
                }

                subscribers = [.. _subscribers];
            }

            foreach (Channel<CaptureProgress> channel in subscribers)
            {
                channel.Writer.TryWrite(progress);
            }

            if (_terminal)
            {
                foreach (Channel<CaptureProgress> channel in subscribers)
                {
                    channel.Writer.TryComplete();
                }
            }
        }

        public async IAsyncEnumerable<CaptureProgress> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Channel<CaptureProgress> channel = Channel.CreateUnbounded<CaptureProgress>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false,
                });

            CaptureProgress[] replay;
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

            foreach (CaptureProgress item in replay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                if (IsTerminal(item.Stage))
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
                await foreach (CaptureProgress item in channel.Reader
                                   .ReadAllAsync(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    yield return item;
                    if (IsTerminal(item.Stage))
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
