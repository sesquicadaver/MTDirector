using System.Buffers;

namespace Mfc.RouterOs.Protocol;

/// <summary>Result of a non-blocking parse step.</summary>
public enum ApiSentenceParseStatus : byte
{
    NeedMoreData = 0,
    Sentence = 1,
    Faulted = 2,
}

/// <summary>
/// Streaming RouterOS sentence parser over <see cref="ReadOnlySequence{T}"/> (PipeReader buffers).
/// Uses <see cref="SequenceReader{T}"/> and <see cref="MemoryPool{T}"/>. No blocking I/O.
/// FAULTED parsers cannot be reused (Read Adapter Spec §7.1).
/// </summary>
public sealed class ApiSentenceParser : IDisposable
{
    private readonly ApiSentenceLimits _limits;
    private readonly MemoryPool<byte> _pool;
    private readonly List<WordRecord> _currentWords = [];
    private IMemoryOwner<byte>? _bufferOwner;
    private int _bufferLength;
    private int _consecutiveEmptySentences;
    private bool _faulted;
    private bool _disposed;
    private RouterOsProtocolError? _fault;
    private ParsePhase _phase = ParsePhase.ReadingPrefix;
    private uint _pendingWordLength;
    private readonly byte[] _prefixScratch = new byte[5];
    private int _prefixScratchLength;

    public ApiSentenceParser(ApiSentenceLimits? limits = null, MemoryPool<byte>? pool = null)
    {
        _limits = limits ?? ApiSentenceLimits.Default;
        _pool = pool ?? MemoryPool<byte>.Shared;
    }

    public bool IsFaulted => _faulted;

    public RouterOsProtocolError? Fault => _fault;

    /// <summary>
    /// Feeds bytes from a pipeline read. Returns when a sentence completes, more data is needed,
    /// or the parser faults. Consumed bytes are sliced out of <paramref name="buffer"/>.
    /// </summary>
    public ApiSentenceParseStatus TryRead(
        ref ReadOnlySequence<byte> buffer,
        out RosSentenceLease? lease,
        out RouterOsProtocolError? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lease = null;
        error = null;

        if (_faulted)
        {
            error = RouterOsProtocolError.AlreadyFaulted();
            return ApiSentenceParseStatus.Faulted;
        }

        SequenceReader<byte> reader = new(buffer);
        while (true)
        {
            switch (_phase)
            {
                case ParsePhase.ReadingPrefix:
                    {
                        if (!TryFillPrefix(ref reader, out error))
                        {
                            buffer = reader.UnreadSequence;
                            if (error is not null)
                            {
                                TransitionToFaulted(error);
                                return ApiSentenceParseStatus.Faulted;
                            }

                            return ApiSentenceParseStatus.NeedMoreData;
                        }

                        break;
                    }

                case ParsePhase.ReadingBody:
                    {
                        if (_pendingWordLength == 0)
                        {
                            _phase = ParsePhase.ReadingPrefix;
                            _prefixScratchLength = 0;
                            if (!TryCompleteSentence(out lease, out error))
                            {
                                TransitionToFaulted(error!);
                                buffer = reader.UnreadSequence;
                                return ApiSentenceParseStatus.Faulted;
                            }

                            buffer = reader.UnreadSequence;
                            return ApiSentenceParseStatus.Sentence;
                        }

                        if (reader.Remaining < _pendingWordLength)
                        {
                            buffer = reader.UnreadSequence;
                            return ApiSentenceParseStatus.NeedMoreData;
                        }

                        if (!EnsureCapacity((int)_pendingWordLength, out error))
                        {
                            TransitionToFaulted(error!);
                            buffer = reader.UnreadSequence;
                            return ApiSentenceParseStatus.Faulted;
                        }

                        Span<byte> dest = _bufferOwner!.Memory.Span.Slice(_bufferLength, (int)_pendingWordLength);
                        if (!reader.TryCopyTo(dest))
                        {
                            error = RouterOsProtocolError.Truncated("Unable to copy word body from sequence.");
                            TransitionToFaulted(error);
                            buffer = reader.UnreadSequence;
                            return ApiSentenceParseStatus.Faulted;
                        }

                        reader.Advance(_pendingWordLength);
                        int wordOffset = _bufferLength;
                        _bufferLength += (int)_pendingWordLength;
                        if (!TryAcceptWord(wordOffset, (int)_pendingWordLength, out error))
                        {
                            TransitionToFaulted(error!);
                            buffer = reader.UnreadSequence;
                            return ApiSentenceParseStatus.Faulted;
                        }

                        _phase = ParsePhase.ReadingPrefix;
                        _prefixScratchLength = 0;
                        _pendingWordLength = 0;
                        break;
                    }

                default:
                    error = RouterOsProtocolError.AlreadyFaulted();
                    TransitionToFaulted(error);
                    buffer = reader.UnreadSequence;
                    return ApiSentenceParseStatus.Faulted;
            }
        }
    }

    /// <summary>
    /// Signals that the transport closed. Incomplete word/prefix becomes a fault
    /// (connection close mid-word).
    /// </summary>
    public ApiSentenceParseStatus Complete(
        out RosSentenceLease? lease,
        out RouterOsProtocolError? error)
    {
        lease = null;
        error = null;
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted)
        {
            error = RouterOsProtocolError.AlreadyFaulted();
            return ApiSentenceParseStatus.Faulted;
        }

        if (_phase == ParsePhase.ReadingPrefix && _prefixScratchLength == 0 && _currentWords.Count == 0)
        {
            return ApiSentenceParseStatus.NeedMoreData;
        }

        error = RouterOsProtocolError.UnexpectedEof(
            _phase == ParsePhase.ReadingBody
                ? "Connection closed mid-word body."
                : "Connection closed mid-length-prefix.");
        TransitionToFaulted(error);
        return ApiSentenceParseStatus.Faulted;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ResetBuffer();
        GC.SuppressFinalize(this);
    }

    private bool TryFillPrefix(ref SequenceReader<byte> reader, out RouterOsProtocolError? error)
    {
        error = null;

        while (_prefixScratchLength < 5 && reader.TryRead(out byte b))
        {
            _prefixScratch[_prefixScratchLength++] = b;
            ApiLengthDecodeStatus decode = ApiWordLengthCodec.TryDecode(
                _prefixScratch.AsSpan(0, _prefixScratchLength),
                (uint)_limits.MaxWordPayloadBytes,
                out uint length,
                out int consumed,
                out RouterOsProtocolError? decodeError);

            switch (decode)
            {
                case ApiLengthDecodeStatus.NeedMoreData:
                    continue;
                case ApiLengthDecodeStatus.Faulted:
                    error = decodeError;
                    return false;
                case ApiLengthDecodeStatus.Success:
                    if (consumed != _prefixScratchLength)
                    {
                        error = RouterOsProtocolError.Truncated("Length prefix consume mismatch.");
                        return false;
                    }

                    _pendingWordLength = length;
                    _phase = ParsePhase.ReadingBody;
                    return true;
            }
        }

        return false;
    }

    private bool TryAcceptWord(int offset, int length, out RouterOsProtocolError? error)
    {
        error = null;
        if (_currentWords.Count >= _limits.MaxWordsPerSentence)
        {
            error = RouterOsProtocolError.TooManyWordsError(_limits.MaxWordsPerSentence);
            return false;
        }

        ReadOnlyMemory<byte> payload = _bufferOwner!.Memory.Slice(offset, length);
        RosWordKind kind = RosWord.Classify(payload.Span);
        _currentWords.Add(new WordRecord(kind, offset, length));
        return true;
    }

    private bool TryCompleteSentence(out RosSentenceLease? lease, out RouterOsProtocolError? error)
    {
        lease = null;
        error = null;

        List<RosWord> words = new(_currentWords.Count);
        List<RosAttributeEntry> attributes = [];
        List<RosAttributeEntry> apiAttributes = [];
        List<RosWord> queries = [];
        RosWord? head = null;
        Memory<byte> owned = _bufferOwner?.Memory ?? Memory<byte>.Empty;
        int payloadBytes = 0;

        foreach (WordRecord record in _currentWords)
        {
            ReadOnlyMemory<byte> payload = owned.Slice(record.Offset, record.Length);
            RosWord word = new(record.Kind, payload);
            words.Add(word);
            payloadBytes += record.Length;
            head ??= word;

            switch (record.Kind)
            {
                case RosWordKind.Attribute:
                    if (!RosAttributeEntry.TryParse(payload, isApiAttribute: false, out RosAttributeEntry attr, out error))
                    {
                        return false;
                    }

                    attributes.Add(attr);
                    break;
                case RosWordKind.ApiAttribute:
                    if (!RosAttributeEntry.TryParse(payload, isApiAttribute: true, out RosAttributeEntry api, out error))
                    {
                        return false;
                    }

                    apiAttributes.Add(api);
                    break;
                case RosWordKind.Query:
                    queries.Add(word);
                    break;
            }
        }

        if (words.Count == 0)
        {
            _consecutiveEmptySentences++;
            if (_consecutiveEmptySentences > _limits.MaxConsecutiveEmptySentences)
            {
                error = RouterOsProtocolError.SentenceTooLargeError(
                    $"Exceeded {_limits.MaxConsecutiveEmptySentences} consecutive empty sentences.");
                return false;
            }
        }
        else
        {
            _consecutiveEmptySentences = 0;
        }

        // Empty sentence still needs a disposable owner for lease symmetry.
        IMemoryOwner<byte> owner = _bufferOwner ?? _pool.Rent(1);
        _bufferOwner = null;
        _bufferLength = 0;
        _currentWords.Clear();

        RosSentence sentence = new(head, words, attributes, apiAttributes, queries, payloadBytes);
        lease = new RosSentenceLease(owner, sentence);
        return true;
    }

    private bool EnsureCapacity(int additionalBytes, out RouterOsProtocolError? error)
    {
        error = null;
        long needed = (long)_bufferLength + additionalBytes;
        if (needed > _limits.MaxSentencePayloadBytes)
        {
            error = RouterOsProtocolError.SentenceTooLargeError(
                $"Sentence payload would exceed {_limits.MaxSentencePayloadBytes} bytes.");
            return false;
        }

        if (_bufferOwner is null)
        {
            _bufferOwner = _pool.Rent(Math.Max(additionalBytes, 1024));
            return true;
        }

        if (_bufferLength + additionalBytes <= _bufferOwner.Memory.Length)
        {
            return true;
        }

        int grow = Math.Max(_bufferOwner.Memory.Length * 2, _bufferLength + additionalBytes);
        grow = Math.Min(grow, _limits.MaxSentencePayloadBytes);
        if (grow < _bufferLength + additionalBytes)
        {
            error = RouterOsProtocolError.SentenceTooLargeError(
                $"Sentence payload would exceed {_limits.MaxSentencePayloadBytes} bytes.");
            return false;
        }

        IMemoryOwner<byte> next = _pool.Rent(grow);
        _bufferOwner.Memory.Span[.._bufferLength].CopyTo(next.Memory.Span);
        _bufferOwner.Dispose();
        _bufferOwner = next;
        return true;
    }

    private void TransitionToFaulted(RouterOsProtocolError error)
    {
        _faulted = true;
        _fault = error;
        _phase = ParsePhase.Faulted;
        ResetBuffer();
        _currentWords.Clear();
    }

    private void ResetBuffer()
    {
        if (_bufferOwner is not null)
        {
            _bufferOwner.Memory.Span.Clear();
            _bufferOwner.Dispose();
            _bufferOwner = null;
        }

        _bufferLength = 0;
    }

    private enum ParsePhase : byte
    {
        ReadingPrefix = 0,
        ReadingBody = 1,
        Faulted = 2,
    }

    private readonly struct WordRecord(RosWordKind kind, int offset, int length)
    {
        public RosWordKind Kind { get; } = kind;

        public int Offset { get; } = offset;

        public int Length { get; } = length;
    }
}
