using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Reflection;
using System.Text;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Redaction;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class RosReadCommandExecutorTests
{
    [Fact]
    public void RegistryCoversEveryCommandIdWithPrintPathsAndProplist()
    {
        Assert.Equal(Enum.GetValues<RosReadCommandId>().Length, RosReadCommandRegistry.All.Count);
        foreach (RosReadCommandId id in Enum.GetValues<RosReadCommandId>())
        {
            RosReadCommandDefinition definition = RosReadCommandRegistry.Get(id);
            Assert.Equal(id, definition.Id);
            Assert.StartsWith("/", definition.FixedPath, StringComparison.Ordinal);
            Assert.EndsWith("/print", definition.FixedPath, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(definition.PropertyProfile.ProplistValue));
            Assert.Equal("print", definition.FixedPath.Split('/')[^1]);
            Assert.True(RosReadCommandRegistry.IsAllowlistedPath(definition.FixedPath));
        }
    }

    [Fact]
    public void RegistryRejectsWriteAndNonPrintPaths()
    {
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/address/add"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/system/script/run"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/export"));
        Assert.False(RosReadCommandRegistry.IsAllowlistedPath("/ip/address/print?extra"));
    }

    [Fact]
    public void ExecutorPublicApiHasNoFreeFormPathParameter()
    {
        MethodInfo? method = typeof(RosReadCommandExecutor).GetMethod(
            nameof(RosReadCommandExecutor.ExecuteAsync),
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(method);
        ParameterInfo[] parameters = method!.GetParameters();
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(string));
        Assert.Contains(parameters, p => p.ParameterType == typeof(RosReadCommandId));
        Assert.Contains(parameters, p => p.ParameterType == typeof(RosSession));
    }

    [Fact]
    public void QueryProfilesAreImmutableAndNotUiBuilt()
    {
        Assert.Equal("all_rows", RosQueryProfile.AllRows.Id);
        Assert.Equal(["=all="], RosQueryProfile.AllRows.WireWords);
        Assert.Equal([("all", "")], RosQueryProfile.AllRows.PrintArguments);
        Assert.Equal(
            ["?static=true", "?dynamic=false", "?#&"],
            RosQueryProfile.StaticRoutes.QueryWords);
    }

    [Fact]
    public void SensitiveFieldRegistryRedactsAndForbidsSecrets()
    {
        Assert.True(SensitiveFieldRegistry.IsForbidden("password"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("secret"));
        Assert.Equal("[REDACTED]", SensitiveFieldRegistry.RedactForLog("comment", "ops note"));
        Assert.Equal("ether1", SensitiveFieldRegistry.RedactForLog("name", "ether1"));
    }

    [Fact]
    public async Task ExecuteAsyncSendsAllowlistedPathProplistAndStaticQuery()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            Assert.Equal("/ip/route/print", request.Command);
            Assert.Contains(request.ApiAttributes, a => a.Name == "proplist" && a.Value.Contains("dst-address", StringComparison.Ordinal));
            Assert.Equal(["?static=true", "?dynamic=false", "?#&"], request.QueryWords);
            await respond.ReplyAsync("!re", request.Tag, [("dst-address", "10.0.0.0/8"), ("gateway", "1.1.1.1"), ("unknown-x", "keep")]);
            await respond.ReplyAsync("!done", request.Tag);
        });

        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            harness.Session,
            RosReadCommandId.Ipv4StaticRoutes);

        Assert.True(result.IsSuccess);
        Assert.Equal(RosReadCommandId.Ipv4StaticRoutes, result.CommandId);
        Assert.Single(result.Records);
        Assert.Equal("10.0.0.0/8", result.Records[0].KnownProperties["dst-address"]);
        Assert.Equal("keep", result.Records[0].RawProperties["unknown-x"]);
        Assert.False(result.Records[0].KnownProperties.ContainsKey("unknown-x"));
    }

    [Fact]
    public async Task TrapBecomesTypedError()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            await respond.ReplyAsync("!trap", request.Tag, [("category", "2"), ("message", "no such item")]);
            await respond.ReplyAsync("!done", request.Tag);
        });

        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            harness.Session,
            RosReadCommandId.SystemIdentity);

        Assert.False(result.IsSuccess);
        Assert.Equal(RosReadCommandExecutor.TrapErrorCode, result.Error!.Code);
        Assert.Contains("no such item", result.Error.Message, StringComparison.Ordinal);
        Assert.Single(result.Error.Traps);
        Assert.False(result.SessionInvalidated);
    }

    [Fact]
    public async Task FatalInvalidatesSession()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (_, respond) =>
        {
            await respond.ReplyAsync("!fatal", tag: 1);
        });

        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            harness.Session,
            RosReadCommandId.SystemIdentity);

        Assert.True(result.SessionInvalidated);
        Assert.True(harness.Session.IsFaulted);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ForbiddenReturnedAttributesAreDropped()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            await respond.ReplyAsync(
                "!re",
                request.Tag,
                [("name", "gw1"), ("password", "should-not-store"), ("extra", "raw")]);
            await respond.ReplyAsync("!done", request.Tag);
        });

        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            harness.Session,
            RosReadCommandId.SystemIdentity);

        Assert.True(result.IsSuccess);
        Assert.Equal("gw1", result.Records[0].KnownProperties["name"]);
        Assert.False(result.Records[0].KnownProperties.ContainsKey("password"));
        Assert.False(result.Records[0].RawProperties.ContainsKey("password"));
        Assert.Equal("raw", result.Records[0].RawProperties["extra"]);
    }

    private sealed class SessionHarness : IAsyncDisposable
    {
        private readonly Pipe _uplink = new();
        private readonly Pipe _downlink = new();
        private readonly CancellationTokenSource _peerCts = new();
        private readonly Task _peerTask;
        private bool _disposed;

        private SessionHarness(Func<ParsedRequest, Responder, Task> handler)
        {
            Session = new RosSession(_downlink.Reader, _uplink.Writer);
            _peerTask = RunPeerAsync(handler, _peerCts.Token);
        }

        public RosSession Session { get; }

        public static Task<SessionHarness> StartAsync(Func<ParsedRequest, Responder, Task> handler)
            => Task.FromResult(new SessionHarness(handler));

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

        public required IReadOnlyList<(string Name, string Value)> ApiAttributes { get; init; }

        public required IReadOnlyList<string> QueryWords { get; init; }

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

            List<(string, string)> apis = [];
            foreach (RosAttributeEntry attr in sentence.ApiAttributes)
            {
                apis.Add((
                    Encoding.ASCII.GetString(attr.Name.Span),
                    Encoding.UTF8.GetString(attr.Value.Span)));
            }

            List<string> queries = [];
            foreach (RosWord query in sentence.Queries)
            {
                queries.Add(Encoding.UTF8.GetString(query.Payload.Span));
            }

            return new ParsedRequest
            {
                Command = command,
                Tag = tag,
                Attributes = attrs,
                ApiAttributes = apis,
                QueryWords = queries,
            };
        }
    }
}
