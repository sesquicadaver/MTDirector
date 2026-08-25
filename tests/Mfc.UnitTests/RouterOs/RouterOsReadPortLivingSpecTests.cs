using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Reflection;
using System.Text;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

/// <summary>Living Spec for P2-04 / issue #280 — production RouterOsReadPort.</summary>
public sealed class RouterOsReadPortLivingSpecTests
{
    [Fact]
    public void Ac1RouterOsReadPortImplementsReadPort()
    {
        Type type = typeof(RouterOsReadPort);
        Assert.True(typeof(IRouterOsReadPort).IsAssignableFrom(type));
        Assert.Contains(type.GetConstructors(), c => c.GetParameters().Length == 1);
    }

    [Fact]
    public void Ac2RoadmapLivingSpecRowReferencesRouterOsReadPort()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("RouterOsReadPort", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-04", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac3ScriptedSessionProbeReturnsIdentityAndSupportState()
    {
        await using SessionHarness harness = await SessionHarness.StartAsync(ProbeHandlerAsync);
        RouterOsProbeResult result = await RouterOsSystemProbe.ProbeAsync(harness.Session);

        Assert.Equal("pilot-chr", result.Identity);
        Assert.Equal(SupportState.Supported, result.SupportState);
    }

    [Fact]
    public void Ac4RouterOsSystemProbeTypeIsInRouterOsAssembly()
    {
        Assert.Equal("Mfc.RouterOs", typeof(RouterOsSystemProbe).Assembly.GetName().Name);
    }

    [Fact]
    public void Ac5ApplicationAssemblyDoesNotReferenceRouterOs()
    {
        Assembly application = typeof(Mfc.Application.AssemblyMarker).Assembly;
        bool referencesRouterOs = application.GetReferencedAssemblies()
            .Any(a => string.Equals(a.Name, "Mfc.RouterOs", StringComparison.Ordinal));
        Assert.False(referencesRouterOs);
    }

    private static async Task ProbeHandlerAsync(ParsedRequest request, Responder respond)
    {
        switch (request.Command)
        {
            case "/system/identity/print":
                await respond.ReplyAsync("!re", request.Tag, [("name", "pilot-chr")]);
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
                        ("uptime", "1h"),
                    ]);
                break;
            case "/system/routerboard/print":
                await respond.ReplyAsync("!trap", request.Tag, [("message", "n/a")]);
                break;
            case "/system/package/print":
                await respond.ReplyAsync(
                    "!re",
                    request.Tag,
                    [("name", "routeros"), ("version", "7.16.2"), ("disabled", "false")]);
                break;
            case "/system/clock/print":
                await respond.ReplyAsync("!re", request.Tag, [("time-zone-name", "UTC")]);
                break;
            case "/ip/service/print":
                await respond.ReplyAsync(
                    "!re",
                    request.Tag,
                    [("name", "api-ssl"), ("port", "8729"), ("disabled", "false")]);
                break;
            default:
                await respond.ReplyAsync("!trap", request.Tag, [("message", request.Command)]);
                break;
        }

        await respond.ReplyAsync("!done", request.Tag);
    }

    private static string RepoRoot()
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
                                await handler(request, responder).ConfigureAwait(false);
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
