using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using Mfc.RouterOs.Protocol;

namespace Mfc.RouterOs.Session;

/// <summary>
/// Asynchronous tagged RouterOS API session over a single duplex pipe.
/// One read loop, serialized writes, bounded pending registry, no reconnect (Spec §11–13).
/// </summary>
public sealed class RosSession : IAsyncDisposable
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly RosSessionOptions _options;
    private readonly PendingCommandRegistry _pending;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _sessionCts = new();
    private readonly Task _readLoop;
    private readonly object _stateGate = new();

    private ulong _nextTag = 1;
    private bool _disposed;
    private bool _faulted;
    private RouterOsProtocolError? _fault;

    public RosSession(PipeReader reader, PipeWriter writer, RosSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);
        _reader = reader;
        _writer = writer;
        _options = options ?? RosSessionOptions.Default;
        _pending = new PendingCommandRegistry(_options.MaxPendingCommands);
        _readLoop = RunReadLoopAsync(_sessionCts.Token);
    }

    public bool IsFaulted
    {
        get
        {
            lock (_stateGate)
            {
                return _faulted;
            }
        }
    }

    public RouterOsProtocolError? Fault
    {
        get
        {
            lock (_stateGate)
            {
                return _fault;
            }
        }
    }

    public int PendingCount => _pending.Count;

    /// <summary>
    /// Executes a tagged command and completes when <c>!done</c> arrives (or fault/timeout/cancel).
    /// </summary>
    public async Task<RosCommandResult> ExecuteAsync(
        string command,
        IEnumerable<(string Name, string Value)>? attributes = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureNotFaulted();

        ulong tag = AllocateTag();
        PendingCommand pending = new()
        {
            Tag = tag,
            CommandId = command,
            StartedAt = DateTimeOffset.UtcNow,
            Deadline = DateTimeOffset.UtcNow + (timeout ?? _options.DefaultCommandTimeout),
        };

        if (!_pending.TryAdd(pending, out RouterOsProtocolError? addError))
        {
            throw new InvalidOperationException(addError.Message);
        }

        using CancellationTokenSource timeoutCts = new(timeout ?? _options.DefaultCommandTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _sessionCts.Token,
            timeoutCts.Token);

        try
        {
            await WriteCommandAsync(command, tag, attributes, linked.Token).ConfigureAwait(false);
            return await pending.Completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsFaulted || _sessionCts.IsCancellationRequested)
        {
            if (pending.Completion.Task.IsCompleted)
            {
                return await pending.Completion.Task.ConfigureAwait(false);
            }

            return CompleteLocal(
                pending,
                RosCommandLifecycle.Faulted,
                Fault ?? new RouterOsProtocolError("API_SESSION_FAULTED", "Session faulted."));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            await CancelCommandAsync(tag, CancellationToken.None).ConfigureAwait(false);
            if (pending.Completion.Task.IsCompleted)
            {
                return await pending.Completion.Task.ConfigureAwait(false);
            }

            return CompleteLocal(
                pending,
                RosCommandLifecycle.TimedOut,
                new RouterOsProtocolError("API_COMMAND_TIMEOUT", $"Command tag={tag} timed out."));
        }
        catch (OperationCanceledException)
        {
            await CancelCommandAsync(tag, CancellationToken.None).ConfigureAwait(false);
            if (pending.Completion.Task.IsCompleted)
            {
                return await pending.Completion.Task.ConfigureAwait(false);
            }

            return CompleteLocal(
                pending,
                RosCommandLifecycle.Cancelled,
                new RouterOsProtocolError("API_COMMAND_CANCELLED", $"Command tag={tag} cancelled."));
        }
        finally
        {
            _ = _pending.TryRemove(tag, out _);
        }
    }

    /// <summary>Sends a targeted <c>/cancel</c> for an active tagged command.</summary>
    public async Task CancelCommandAsync(ulong targetTag, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_pending.TryGet(targetTag, out PendingCommand? target))
        {
            return;
        }

        target.Lifecycle = RosCommandLifecycle.CancelRequested;
        ulong cancelTag = AllocateTag();
        PendingCommand cancelPending = new()
        {
            Tag = cancelTag,
            CommandId = "/cancel",
            StartedAt = DateTimeOffset.UtcNow,
            Deadline = DateTimeOffset.UtcNow + _options.CancelGracePeriod,
        };
        if (!_pending.TryAdd(cancelPending, out _))
        {
            // Still send /cancel; registry pressure is surfaced on the next ExecuteAsync.
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ArrayBufferWriter<byte> buffer = new();
            List<RosAttributeEntry> attrs =
            [
                new(
                    Encoding.ASCII.GetBytes("tag"),
                    Encoding.ASCII.GetBytes(targetTag.ToString(CultureInfo.InvariantCulture)),
                    isApiAttribute: false),
            ];
            List<RosAttributeEntry> apis =
            [
                new(
                    Encoding.ASCII.GetBytes("tag"),
                    Encoding.ASCII.GetBytes(cancelTag.ToString(CultureInfo.InvariantCulture)),
                    isApiAttribute: true),
            ];
            ApiSentenceEncoder.Encode(
                buffer,
                "/cancel"u8,
                CollectionsMarshal.AsSpan(attrs),
                CollectionsMarshal.AsSpan(apis));
            await _writer.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _sessionCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Idempotent dispose: transport may already be completed.
        }

        try
        {
            await _writer.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Idempotent dispose.
        }

        try
        {
            await _readLoop.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Read loop observes cancellation/completion.
        }

        CompleteAllPending(
            new RouterOsProtocolError("API_SESSION_CLOSED", "Session closed."),
            RosCommandLifecycle.Cancelled);

        _writeLock.Dispose();
        _sessionCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private static RosCommandResult CompleteLocal(
        PendingCommand pending,
        RosCommandLifecycle lifecycle,
        RouterOsProtocolError error)
    {
        pending.Lifecycle = lifecycle;
        RosCommandResult result = new()
        {
            Tag = pending.Tag,
            Lifecycle = lifecycle,
            Records = pending.Records.ToArray(),
            Traps = pending.Traps.ToArray(),
            HadEmpty = pending.HadEmpty,
            Error = error,
        };
        pending.Completion.TrySetResult(result);
        return result;
    }

    private async Task WriteCommandAsync(
        string command,
        ulong tag,
        IEnumerable<(string Name, string Value)>? attributes,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ArrayBufferWriter<byte> buffer = new();
            List<RosAttributeEntry> attrs = [];
            if (attributes is not null)
            {
                foreach ((string name, string value) in attributes)
                {
                    attrs.Add(new RosAttributeEntry(
                        Encoding.ASCII.GetBytes(name),
                        Encoding.UTF8.GetBytes(value),
                        isApiAttribute: false));
                }
            }

            List<RosAttributeEntry> apis =
            [
                new(
                    Encoding.ASCII.GetBytes("tag"),
                    Encoding.ASCII.GetBytes(tag.ToString(CultureInfo.InvariantCulture)),
                    isApiAttribute: true),
            ];

            ApiSentenceEncoder.Encode(
                buffer,
                Encoding.ASCII.GetBytes(command),
                CollectionsMarshal.AsSpan(attrs),
                CollectionsMarshal.AsSpan(apis));
            await _writer.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task RunReadLoopAsync(CancellationToken cancellationToken)
    {
        using ApiSentenceParser parser = new(_options.SentenceLimits);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult read = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = read.Buffer;
                try
                {
                    while (buffer.Length > 0)
                    {
                        ApiSentenceParseStatus status = parser.TryRead(
                            ref buffer,
                            out RosSentenceLease? lease,
                            out RouterOsProtocolError? error);

                        if (status == ApiSentenceParseStatus.NeedMoreData)
                        {
                            break;
                        }

                        if (status == ApiSentenceParseStatus.Faulted)
                        {
                            TransitionToFaulted(error ?? RouterOsProtocolError.AlreadyFaulted());
                            return;
                        }

                        ulong? cancelTarget = null;
                        using (lease)
                        {
                            if (lease is not null)
                            {
                                cancelTarget = RouteSentence(lease.Sentence);
                                if (IsFaulted)
                                {
                                    return;
                                }
                            }
                        }

                        if (cancelTarget is ulong tagToCancel)
                        {
                            await CancelCommandAsync(tagToCancel, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (read.IsCompleted)
                    {
                        ApiSentenceParseStatus complete = parser.Complete(out _, out RouterOsProtocolError? eofError);
                        if (complete == ApiSentenceParseStatus.Faulted)
                        {
                            TransitionToFaulted(
                                eofError ?? new RouterOsProtocolError(
                                    RouterOsProtocolError.UnexpectedEndOfStream,
                                    "Connection closed."));
                        }
                        else
                        {
                            CompleteAllPending(
                                new RouterOsProtocolError("API_SESSION_CLOSED", "Connection closed."),
                                RosCommandLifecycle.Cancelled);
                        }

                        return;
                    }
                }
                finally
                {
                    _reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Session disposal.
        }
        catch (Exception ex)
        {
            TransitionToFaulted(new RouterOsProtocolError("API_READ_LOOP_FAULT", ex.Message));
        }
    }

    /// <summary>Routes a reply sentence. Returns a tag that must be cancelled (limit exceeded).</summary>
    private ulong? RouteSentence(RosSentence sentence)
    {
        if (sentence.IsEmptySentence || sentence.Head is null)
        {
            return null;
        }

        RosWord head = sentence.Head.Value;
        if (head.Kind != RosWordKind.Reply)
        {
            TransitionToFaulted(
                new RouterOsProtocolError("API_UNEXPECTED_WORD", "Expected reply marker as sentence head."));
            return null;
        }

        if (!RosWord.TryDecodeStrictAscii(head.Payload.Span, out string? marker) || marker is null)
        {
            TransitionToFaulted(
                new RouterOsProtocolError("API_INVALID_REPLY_MARKER", "Reply marker is not strict ASCII."));
            return null;
        }

        bool hasTag = sentence.TryGetUniqueTag(out ReadOnlyMemory<byte> tagBytes, out RouterOsProtocolError? tagError);
        if (tagError is not null)
        {
            TransitionToFaulted(tagError);
            return null;
        }

        if (marker == "!fatal")
        {
            TransitionToFaulted(new RouterOsProtocolError("API_FATAL", "RouterOS sent !fatal."));
            return null;
        }

        if (!hasTag)
        {
            TransitionToFaulted(
                new RouterOsProtocolError(
                    RouterOsProtocolError.UnknownReplyTag,
                    "Untagged reply is forbidden in READY state."));
            return null;
        }

        if (!ulong.TryParse(
                Encoding.ASCII.GetString(tagBytes.Span),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out ulong tag))
        {
            TransitionToFaulted(
                new RouterOsProtocolError(
                    RouterOsProtocolError.UnknownReplyTag,
                    "Reply .tag is not a decimal ulong."));
            return null;
        }

        if (!_pending.TryGet(tag, out PendingCommand? pending))
        {
            TransitionToFaulted(
                new RouterOsProtocolError(
                    RouterOsProtocolError.UnknownReplyTag,
                    $"No pending command for tag={tag}."));
            return null;
        }

        switch (marker)
        {
            case "!re":
                return AppendRecord(pending, sentence) ? tag : null;
            case "!empty":
                pending.HadEmpty = true;
                return null;
            case "!trap":
                pending.Traps.Add(new RosTrap
                {
                    Attributes = sentence.Attributes
                        .Select(a => new RosAttributeEntry(a.Name.ToArray(), a.Value.ToArray(), a.IsApiAttribute))
                        .ToArray(),
                });
                return null;
            case "!done":
                CompleteCommand(pending, RosCommandLifecycle.Completed, error: null);
                return null;
            default:
                TransitionToFaulted(
                    new RouterOsProtocolError("API_UNKNOWN_REPLY_MARKER", $"Unsupported reply marker '{marker}'."));
                return null;
        }
    }

    /// <returns><c>true</c> when the command exceeded collector limits and needs <c>/cancel</c>.</returns>
    private bool AppendRecord(PendingCommand pending, RosSentence sentence)
    {
        List<RosAttributeEntry> attrs = sentence.Attributes
            .Select(a => new RosAttributeEntry(a.Name.ToArray(), a.Value.ToArray(), a.IsApiAttribute))
            .ToList();
        List<RosAttributeEntry> apis = sentence.ApiAttributes
            .Select(a => new RosAttributeEntry(a.Name.ToArray(), a.Value.ToArray(), a.IsApiAttribute))
            .ToList();
        List<RosWord> words = sentence.Words
            .Select(w => new RosWord(w.Kind, w.Payload.ToArray()))
            .ToList();
        RosSentence copy = new(
            words.Count > 0 ? words[0] : null,
            words,
            attrs,
            apis,
            sentence.Queries.Select(q => new RosWord(q.Kind, q.Payload.ToArray())).ToList(),
            sentence.PayloadBytes);

        pending.PayloadBytes += sentence.PayloadBytes;
        if (pending.Records.Count + 1 > _options.MaxRecordsPerCommand
            || pending.PayloadBytes > _options.MaxPayloadBytesPerCommand)
        {
            pending.Lifecycle = RosCommandLifecycle.LimitExceeded;
            return true;
        }

        pending.Records.Add(copy);
        return false;
    }

    private void CompleteCommand(PendingCommand pending, RosCommandLifecycle lifecycle, RouterOsProtocolError? error)
    {
        pending.Lifecycle = lifecycle;
        pending.Completion.TrySetResult(new RosCommandResult
        {
            Tag = pending.Tag,
            Lifecycle = lifecycle,
            Records = pending.Records.ToArray(),
            Traps = pending.Traps.ToArray(),
            HadEmpty = pending.HadEmpty,
            Error = error,
        });
        _ = _pending.TryRemove(pending.Tag, out _);
    }

    private void TransitionToFaulted(RouterOsProtocolError error)
    {
        lock (_stateGate)
        {
            if (_faulted)
            {
                return;
            }

            _faulted = true;
            _fault = error;
        }

        CompleteAllPending(error, RosCommandLifecycle.Faulted);
        _sessionCts.Cancel();
    }

    private void CompleteAllPending(RouterOsProtocolError error, RosCommandLifecycle lifecycle)
    {
        foreach (PendingCommand pending in _pending.Snapshot())
        {
            pending.Lifecycle = lifecycle;
            pending.Completion.TrySetResult(new RosCommandResult
            {
                Tag = pending.Tag,
                Lifecycle = lifecycle,
                Records = pending.Records.ToArray(),
                Traps = pending.Traps.ToArray(),
                HadEmpty = pending.HadEmpty,
                Error = error,
            });
        }

        _pending.Clear();
    }

    private ulong AllocateTag()
    {
        lock (_stateGate)
        {
            if (_nextTag == 0)
            {
                TransitionToFaulted(
                    new RouterOsProtocolError("API_TAG_OVERFLOW", "Tag generator overflowed."));
                throw new InvalidOperationException("Tag generator overflowed.");
            }

            ulong tag = _nextTag++;
            return tag;
        }
    }

    private void EnsureNotFaulted()
    {
        lock (_stateGate)
        {
            if (_faulted)
            {
                throw new InvalidOperationException(_fault?.Message ?? "Session is FAULTED.");
            }
        }
    }
}
