using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class RosSessionTests
{
    [Fact]
    public async Task ExecuteAsyncRoutesOutOfOrderTaggedReplies()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            Assert.Equal("/ip/address/print", request.Command);
            ulong tag = request.Tag;
            // Out-of-order: !done for a later injection arrives after !re.
            await respond.ReplyAsync("!re", tag, [("address", "10.0.0.1/24")]);
            await respond.ReplyAsync("!done", tag);
        });

        RosCommandResult result = await harness.Session.ExecuteAsync("/ip/address/print");
        Assert.Equal(RosCommandLifecycle.Completed, result.Lifecycle);
        Assert.Single(result.Records);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ConcurrentCommandsReceiveDistinctTagsAndMatchingReplies()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            // Delay first tag so second can complete first (out-of-order completion).
            if (request.Tag == 1)
            {
                await Task.Delay(50);
            }

            await respond.ReplyAsync("!re", request.Tag, [("id", request.Tag.ToString(CultureInfo.InvariantCulture))]);
            await respond.ReplyAsync("!done", request.Tag);
        });

        Task<RosCommandResult> first = harness.Session.ExecuteAsync("/system/resource/print");
        Task<RosCommandResult> second = harness.Session.ExecuteAsync("/system/identity/print");
        RosCommandResult[] results = await Task.WhenAll(first, second);

        Assert.Equal(1UL, results[0].Tag);
        Assert.Equal(2UL, results[1].Tag);
        Assert.All(results, r => Assert.Equal(RosCommandLifecycle.Completed, r.Lifecycle));
    }

    [Fact]
    public async Task UntaggedReplyFaultsSession()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (_, respond) =>
        {
            await respond.ReplyUntaggedAsync("!done");
        });

        RosCommandResult result = await harness.Session.ExecuteAsync("/system/identity/print");
        Assert.Equal(RosCommandLifecycle.Faulted, result.Lifecycle);
        Assert.Equal(RouterOsProtocolError.UnknownReplyTag, result.Error!.Code);
        Assert.True(harness.Session.IsFaulted);
    }

    [Fact]
    public async Task UnknownTagFaultsSession()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (_, respond) =>
        {
            await respond.ReplyAsync("!done", tag: 999);
        });

        RosCommandResult result = await harness.Session.ExecuteAsync("/system/identity/print");
        Assert.Equal(RosCommandLifecycle.Faulted, result.Lifecycle);
        Assert.Equal(RouterOsProtocolError.UnknownReplyTag, result.Error!.Code);
    }

    [Fact]
    public async Task CancelCommandSendsTargetedCancel()
    {
        TaskCompletionSource<ulong> commandStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ulong> sawCancel = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            if (request.Command == "/cancel")
            {
                foreach ((string Name, string Value) attr in request.Attributes)
                {
                    if (attr.Name == "tag"
                        && ulong.TryParse(attr.Value, CultureInfo.InvariantCulture, out ulong targetTag))
                    {
                        sawCancel.TrySetResult(targetTag);
                        break;
                    }
                }

                await respond.ReplyAsync("!done", request.Tag);
                return;
            }

            commandStarted.TrySetResult(request.Tag);
            ulong cancelled = await sawCancel.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(request.Tag, cancelled);
            await respond.ReplyAsync("!trap", request.Tag, [("category", "2")]);
            await respond.ReplyAsync("!done", request.Tag);
        });

        Task<RosCommandResult> execute = harness.Session.ExecuteAsync("/interface/print");
        ulong tag = await commandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await harness.Session.CancelCommandAsync(tag);
        RosCommandResult result = await execute.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RosCommandLifecycle.Completed, result.Lifecycle);
        Assert.NotEmpty(result.Traps);
        Assert.True(sawCancel.Task.IsCompletedSuccessfully);
        Assert.Equal(tag, await sawCancel.Task);
    }

    [Fact]
    public async Task PendingRegistryIsBounded()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(
            async (_, _) => await Task.Delay(Timeout.Infinite),
            new RosSessionOptions { MaxPendingCommands = 2, DefaultCommandTimeout = TimeSpan.FromSeconds(5) });

        _ = harness.Session.ExecuteAsync("/a");
        _ = harness.Session.ExecuteAsync("/b");
        await Task.Delay(20);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.ExecuteAsync("/c"));
    }

    [Fact]
    public async Task ConnectionCloseCompletesPendingCommands()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (_, _) =>
        {
            // Never reply; harness dispose closes pipes.
            await Task.Delay(Timeout.Infinite);
        });

        Task<RosCommandResult> execute = harness.Session.ExecuteAsync(
            "/system/identity/print",
            timeout: TimeSpan.FromSeconds(5));
        await harness.ClosePeerAsync();
        RosCommandResult result = await execute;
        Assert.True(
            result.Lifecycle is RosCommandLifecycle.Faulted
                or RosCommandLifecycle.Cancelled
                or RosCommandLifecycle.TimedOut);
    }

    [Fact]
    public async Task DisposeIsIdempotent()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            await respond.ReplyAsync("!done", request.Tag);
        });

        _ = await harness.Session.ExecuteAsync("/system/identity/print");
        await harness.Session.DisposeAsync();
        await harness.Session.DisposeAsync();
    }

    [Fact]
    public async Task StressOutOfOrderReplies()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            await Task.Delay(Random.Shared.Next(0, 20));
            if (Random.Shared.Next(0, 3) == 0)
            {
                await respond.ReplyAsync("!empty", request.Tag);
            }
            else
            {
                await respond.ReplyAsync("!re", request.Tag, [("n", request.Tag.ToString(CultureInfo.InvariantCulture))]);
            }

            await respond.ReplyAsync("!done", request.Tag);
        });

        Task<RosCommandResult>[] tasks = Enumerable.Range(0, 12)
            .Select(i => harness.Session.ExecuteAsync($"/cmd/{i}"))
            .ToArray();
        RosCommandResult[] results = await Task.WhenAll(tasks);
        Assert.Equal(12, results.Length);
        Assert.Equal(12, results.Select(r => r.Tag).Distinct().Count());
        Assert.All(results, r => Assert.Equal(RosCommandLifecycle.Completed, r.Lifecycle));
    }

    private sealed class SessionHarness : IAsyncDisposable
    {
        private readonly Pipe _uplink = new();
        private readonly Pipe _downlink = new();
        private readonly CancellationTokenSource _peerCts = new();
        private readonly Task _peerTask;
        private bool _disposed;

        private SessionHarness(Func<ParsedRequest, Responder, Task> handler, RosSessionOptions options)
        {
            Session = new RosSession(_downlink.Reader, _uplink.Writer, options);
            _peerTask = RunPeerAsync(handler, _peerCts.Token);
        }

        public RosSession Session { get; }

        public static Task<SessionHarness> StartAsync(
            Func<ParsedRequest, Responder, Task> handler,
            RosSessionOptions? options = null)
            => Task.FromResult(new SessionHarness(handler, options ?? RosSessionOptions.Default));

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
                                // Parallel handlers so out-of-order replies can be tested.
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
    }

    private sealed class Responder : IDisposable
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
            List<ReadOnlyMemory<byte>> words =
            [
                Encoding.ASCII.GetBytes(marker),
            ];
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

    private readonly struct ParsedRequest
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
