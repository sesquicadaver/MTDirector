using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs.FaultInjection;

/// <summary>Pipe-backed RosSession peer for M1-33 session fault scenarios (no network).</summary>
public sealed class FaultInjectionSessionHarness : IAsyncDisposable
{
    private readonly Pipe _uplink = new();
    private readonly Pipe _downlink = new();
    private readonly CancellationTokenSource _peerCts = new();
    private readonly Task _peerTask;
    private bool _disposed;

    private FaultInjectionSessionHarness(Func<ParsedRequest, Responder, Task> handler, RosSessionOptions options)
    {
        Session = new RosSession(_downlink.Reader, _uplink.Writer, options);
        _peerTask = RunPeerAsync(handler, _peerCts.Token);
    }

    public RosSession Session { get; }

    public static Task<FaultInjectionSessionHarness> StartAsync(
        Func<ParsedRequest, Responder, Task> handler,
        RosSessionOptions? options = null)
        => Task.FromResult(new FaultInjectionSessionHarness(handler, options ?? RosSessionOptions.Default));

    public async Task ClosePeerAsync()
    {
        await _peerCts.CancelAsync();
        await _uplink.Writer.CompleteAsync();
        await _downlink.Writer.CompleteAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _peerCts.CancelAsync();
        await Session.DisposeAsync();
        await _uplink.Writer.CompleteAsync();
        await _downlink.Writer.CompleteAsync();
        try
        {
            await _peerTask;
        }
        catch (Exception)
        {
            // Peer observes cancellation.
        }

        _peerCts.Dispose();
    }

    private async Task RunPeerAsync(Func<ParsedRequest, Responder, Task> handler, CancellationToken ct)
    {
        using ApiSentenceParser parser = new();
        using Responder responder = new(_downlink.Writer);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ReadResult read = await _uplink.Reader.ReadAsync(ct).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = read.Buffer;
                try
                {
                    while (buffer.Length > 0)
                    {
                        ApiSentenceParseStatus status = parser.TryRead(
                            ref buffer,
                            out RosSentenceLease? lease,
                            out _);
                        if (status == ApiSentenceParseStatus.NeedMoreData)
                        {
                            break;
                        }

                        if (status != ApiSentenceParseStatus.Sentence || lease is null)
                        {
                            return;
                        }

                        using (lease)
                        {
                            ParsedRequest request = ParsedRequest.From(lease.Sentence);
                            _ = Task.Run(() => handler(request, responder), ct);
                        }
                    }

                    if (read.IsCompleted)
                    {
                        return;
                    }
                }
                finally
                {
                    _uplink.Reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public sealed class Responder : IDisposable
    {
        private readonly PipeWriter _writer;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public Responder(PipeWriter writer) => _writer = writer;

        public async Task ReplyAsync(
            string marker,
            ulong tag,
            IEnumerable<(string Name, string Value)>? attributes = null)
        {
            ArrayBufferWriter<byte> buffer = new();
            List<ReadOnlyMemory<byte>> words = [Encoding.ASCII.GetBytes(marker)];
            if (attributes is not null)
            {
                foreach ((string name, string value) in attributes)
                {
                    words.Add(Encoding.UTF8.GetBytes($"={name}={value}"));
                }
            }

            words.Add(Encoding.ASCII.GetBytes($".tag={tag.ToString(CultureInfo.InvariantCulture)}"));
            ApiSentenceEncoder.EncodeWords(buffer, words.ToArray());
            await WriteAsync(buffer.WrittenMemory).ConfigureAwait(false);
        }

        public async Task ReplyUntaggedAsync(string marker)
        {
            ArrayBufferWriter<byte> buffer = new();
            ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes(marker),
            });
            await WriteAsync(buffer.WrittenMemory).ConfigureAwait(false);
        }

        public async Task WriteRawAsync(ReadOnlyMemory<byte> payload)
            => await WriteAsync(payload).ConfigureAwait(false);

        public void Dispose() => _gate.Dispose();

        private async Task WriteAsync(ReadOnlyMemory<byte> payload)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _writer.WriteAsync(payload).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public readonly struct ParsedRequest
    {
        public required string Command { get; init; }

        public required ulong Tag { get; init; }

        public required IReadOnlyList<(string Name, string Value)> Attributes { get; init; }

        public static ParsedRequest From(RosSentence sentence)
        {
            string command = Encoding.ASCII.GetString(sentence.Head!.Value.Payload.Span);
            Assert.True(sentence.TryGetUniqueTag(out ReadOnlyMemory<byte> tagBytes, out _));
            ulong tag = ulong.Parse(Encoding.ASCII.GetString(tagBytes.Span), CultureInfo.InvariantCulture);
            List<(string, string)> attrs = [];
            foreach (RosAttributeEntry attr in sentence.Attributes)
            {
                attrs.Add((
                    Encoding.ASCII.GetString(attr.Name.Span),
                    Encoding.UTF8.GetString(attr.Value.Span)));
            }

            return new ParsedRequest
            {
                Command = command,
                Tag = tag,
                Attributes = attrs,
            };
        }
    }
}
