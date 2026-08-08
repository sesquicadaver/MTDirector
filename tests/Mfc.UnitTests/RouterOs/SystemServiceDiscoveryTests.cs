using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class SystemServiceDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsyncMapsSystemAndApiSslWithoutExport()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(async (request, respond) =>
        {
            Assert.DoesNotContain("export", request.Command, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("show-sensitive", request.Command, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(request.ApiAttributes, a => a.Name == "proplist");

            switch (request.Command)
            {
                case "/system/identity/print":
                    await respond.ReplyAsync("!re", request.Tag, [("name", "lab-gw1")]);
                    break;
                case "/system/resource/print":
                    await respond.ReplyAsync(
                        "!re",
                        request.Tag,
                        [
                            ("version", "7.16.2"),
                            ("architecture-name", "x86_64"),
                            ("board-name", "CHR"),
                            ("platform", "MikroTik"),
                            ("build-time", "2024-11-26"),
                            ("uptime", "5h"),
                            ("free-hdd-space", "should-be-raw"),
                        ]);
                    break;
                case "/system/routerboard/print":
                    await respond.ReplyAsync("!trap", request.Tag, [("category", "1"), ("message", "not supported")]);
                    break;
                case "/system/package/print":
                    await respond.ReplyAsync(
                        "!re",
                        request.Tag,
                        [(".id", "*1"), ("name", "routeros"), ("version", "7.16.2"), ("disabled", "false")]);
                    break;
                case "/system/clock/print":
                    await respond.ReplyAsync(
                        "!re",
                        request.Tag,
                        [("time", "12:00:00"), ("date", "2026-08-08"), ("time-zone-name", "UTC")]);
                    break;
                case "/ip/service/print":
                    await respond.ReplyAsync(
                        "!re",
                        request.Tag,
                        [
                            ("name", "www"),
                            ("port", "80"),
                            ("disabled", "true"),
                        ]);
                    await respond.ReplyAsync(
                        "!re",
                        request.Tag,
                        [
                            ("name", "api-ssl"),
                            ("port", "8729"),
                            ("address", "10.0.0.0/8"),
                            ("certificate", "controller-api"),
                            ("tls-version", "only-1.2"),
                            ("disabled", "false"),
                            ("mystery", "raw-bag"),
                        ]);
                    break;
                default:
                    await respond.ReplyAsync("!trap", request.Tag, [("message", $"unexpected {request.Command}")]);
                    break;
            }

            await respond.ReplyAsync("!done", request.Tag);
        });

        SystemServiceDiscoveryResult result = await SystemServiceDiscovery.DiscoverAsync(harness.Session);

        Assert.Equal("lab-gw1", result.Identity.Name);
        Assert.Equal("7.16.2", result.Resource.Version);
        Assert.Equal("5h", result.Resource.Uptime);
        Assert.Equal("should-be-raw", result.Resource.RawProperties["free-hdd-space"]);
        Assert.False(result.Routerboard.Available);
        Assert.Single(result.Packages);
        Assert.Equal("UTC", result.Clock.TimeZoneName);
        Assert.True(result.ApiSsl.Found);
        Assert.False(result.ApiSsl.Disabled);
        Assert.Equal("8729", result.ApiSsl.Port);
        Assert.Equal("10.0.0.0/8", result.ApiSsl.AddressPrefixes);
        Assert.Equal("controller-api", result.ApiSsl.Certificate);
        Assert.Equal("raw-bag", result.ApiSsl.RawProperties["mystery"]);

        Assert.DoesNotContain("5h", result.ConfigurationHashMaterial.Values);
        Assert.False(result.ConfigurationHashMaterial.ContainsKey("resource.uptime"));
        Assert.Equal("7.16.2", result.ConfigurationHashMaterial["resource.version"]);
        Assert.Equal("8729", result.ConfigurationHashMaterial["api-ssl.port"]);
    }

    [Fact]
    public void SanitizedFixtureExistsWithoutSecrets()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "system-service-discovery.sanitized.json");
        Assert.True(File.Exists(path));

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = doc.RootElement;
        Assert.Equal("mfc.system-service-discovery.sanitized/v1", root.GetProperty("schema").GetString());
        Assert.Equal("lab-gw1", root.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal("8729", root.GetProperty("apiSsl").GetProperty("port").GetString());
        Assert.Equal("1d2h3m4s", root.GetProperty("resource").GetProperty("uptime").GetString());

        string payload = string.Concat(
            root.GetProperty("identity").ToString(),
            root.GetProperty("resource").ToString(),
            root.GetProperty("routerboard").ToString(),
            root.GetProperty("packages").ToString(),
            root.GetProperty("clock").ToString(),
            root.GetProperty("apiSsl").ToString());
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-key", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/export", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemClockIsAllowlistedPrintOnly()
    {
        RosReadCommandDefinition clock = RosReadCommandRegistry.Get(RosReadCommandId.SystemClock);
        Assert.Equal("/system/clock/print", clock.FixedPath);
        Assert.Contains("time-zone-name", clock.PropertyProfile.ProplistValue, StringComparison.Ordinal);
        Assert.True(SystemServiceDiscovery.IsSystemServiceCommand(RosReadCommandId.SystemClock));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
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

        public required IReadOnlyList<(string Name, string Value)> ApiAttributes { get; init; }

        public static ParsedRequest From(RosSentence sentence)
        {
            string command = Encoding.ASCII.GetString(sentence.Head!.Value.Payload.Span);
            Assert.True(sentence.TryGetUniqueTag(out ReadOnlyMemory<byte> tagBytes, out _));
            ulong tag = ulong.Parse(Encoding.ASCII.GetString(tagBytes.Span), CultureInfo.InvariantCulture);
            List<(string, string)> apis = [];
            foreach (RosAttributeEntry attr in sentence.ApiAttributes)
            {
                apis.Add((
                    Encoding.ASCII.GetString(attr.Name.Span),
                    Encoding.UTF8.GetString(attr.Value.Span)));
            }

            return new ParsedRequest
            {
                Command = command,
                Tag = tag,
                ApiAttributes = apis,
            };
        }
    }
}
