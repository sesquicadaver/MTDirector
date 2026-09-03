using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Transport;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class ApiSslConnectionTests
{
    [Fact]
    public async Task ConnectAsyncWithInternalCaReturnsSessionAfterLogin()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(LoginBehavior.Accept);
        using SecretLease password = new("secret"u8);
        await using AuthenticatedRosConnection connection = await AuthenticatedRosConnection.ConnectAsync(
            new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.InternalCa,
                TrustedRootCertificates = server.TrustedRoots,
                CertificateRevocationMode = X509RevocationMode.NoCheck,
            });

        Assert.False(connection.Session.IsFaulted);
        Assert.True(connection.NegotiatedProtocol is SslProtocols.Tls12 or SslProtocols.Tls13);
    }

    [Fact]
    public async Task ConnectAsyncWithSpkiPinAcceptsMatchingCertificate()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(LoginBehavior.Accept);
        Hash256 pin = ApiSslCertificateValidator.ComputeSpkiSha256(server.ServerCertificate);
        using SecretLease password = new("secret"u8);
        await using AuthenticatedRosConnection connection = await AuthenticatedRosConnection.ConnectAsync(
            new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.SpkiPin,
                PinnedSpkiSha256 = pin,
            });

        Assert.NotNull(connection.Session);
    }

    [Fact]
    public async Task ConnectAsyncRejectsExpiredCertificate()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(
            LoginBehavior.Accept,
            certificateValidity: CertificateValidity.Expired);
        using SecretLease password = new("secret"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.InternalCa,
                TrustedRootCertificates = server.TrustedRoots,
                CertificateRevocationMode = X509RevocationMode.NoCheck,
            }));

        Assert.Equal(ApiSslErrors.CertificateExpired, ex.Code);
    }

    [Fact]
    public async Task ConnectAsyncRejectsHostnameMismatchWithDedicatedCode()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(
            LoginBehavior.Accept,
            sanHost: "router.lab");
        using SecretLease password = new("secret"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.InternalCa,
                TrustedRootCertificates = server.TrustedRoots,
                CertificateRevocationMode = X509RevocationMode.NoCheck,
            }));

        Assert.Equal(ApiSslErrors.HostnameMismatch, ex.Code);
    }

    [Fact]
    public async Task ConnectAsyncRejectsSpkiMismatch()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(LoginBehavior.Accept);
        using SecretLease password = new("secret"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.SpkiPin,
                PinnedSpkiSha256 = Hash256.Create(Enumerable.Repeat((byte)7, 32).ToArray()),
            }));

        Assert.Equal(ApiSslErrors.CertificateMismatch, ex.Code);
    }

    [Fact]
    public async Task ConnectAsyncAuthenticationFailureDoesNotRetry()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(LoginBehavior.Reject);
        using SecretLease password = new("bad"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.InternalCa,
                TrustedRootCertificates = server.TrustedRoots,
                CertificateRevocationMode = X509RevocationMode.NoCheck,
            }));

        Assert.Equal(ApiSslErrors.AuthenticationFailed, ex.Code);
        Assert.Equal(1, server.LoginAttempts);
    }

    [Fact]
    public async Task ConnectAsyncRejectsLegacyChallengeAuth()
    {
        await using TestApiSslServer server = await TestApiSslServer.StartAsync(LoginBehavior.LegacyChallenge);
        using SecretLease password = new("secret"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.InternalCa,
                TrustedRootCertificates = server.TrustedRoots,
                CertificateRevocationMode = X509RevocationMode.NoCheck,
            }));

        Assert.Equal(ApiSslErrors.UnsupportedLegacyAuth, ex.Code);
    }

    [Fact]
    public async Task ConnectAsyncForbidsPlainApiPort8728()
    {
        using SecretLease password = new("secret"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = 8728,
                Username = "admin",
                Password = password,
                TrustMode = CertificateTrustMode.SpkiPin,
                PinnedSpkiSha256 = Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray()),
            }));

        Assert.Equal(ApiSslErrors.PlainApiForbidden, ex.Code);
    }

    [Fact]
    public void ExceptionAndLogsDoNotEmbedPassword()
    {
        ApiSslException ex = new(ApiSslErrors.AuthenticationFailed, "RouterOS rejected login credentials.");
        Assert.DoesNotContain("secret", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionRouterOsAssemblyHasNoPlainApiPortConstant()
    {
        string? sourceRoot = FindRepoFile("src/Mfc.RouterOs/Transport/AuthenticatedRosConnection.cs");
        Assert.NotNull(sourceRoot);
        string transportDir = Path.GetDirectoryName(sourceRoot)!;
        foreach (string file in Directory.EnumerateFiles(transportDir, "*.cs"))
        {
            string text = File.ReadAllText(file);
            // Forbidden: advertising plain API as a supported default endpoint.
            Assert.DoesNotContain("const ushort PlainApi", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DefaultPlainApiPort", text, StringComparison.Ordinal);
        }
    }

    private static string? FindRepoFile(string relative)
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        return null;
    }
}

internal enum LoginBehavior
{
    Accept,
    Reject,
    LegacyChallenge,
}

internal enum CertificateValidity
{
    Valid,
    Expired,
}

internal sealed class TestApiSslServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly LoginBehavior _behavior;
    private int _loginAttempts;

    private TestApiSslServer(
        TcpListener listener,
        X509Certificate2 ca,
        X509Certificate2 serverCertificate,
        LoginBehavior behavior)
    {
        _listener = listener;
        _behavior = behavior;
        CaCertificate = ca;
        ServerCertificate = serverCertificate;
        TrustedRoots = [ca];
        Port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
        _loop = AcceptLoopAsync(_cts.Token);
    }

    public ushort Port { get; }

    public X509Certificate2 CaCertificate { get; }

    public X509Certificate2 ServerCertificate { get; }

    public X509Certificate2Collection TrustedRoots { get; }

    public int LoginAttempts => Volatile.Read(ref _loginAttempts);

    public static Task<TestApiSslServer> StartAsync(
        LoginBehavior behavior,
        CertificateValidity certificateValidity = CertificateValidity.Valid,
        string? sanHost = null)
    {
        (X509Certificate2 ca, X509Certificate2 server) = CreateCerts(certificateValidity, sanHost ?? "127.0.0.1");
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return Task.FromResult(new TestApiSslServer(listener, ca, server, behavior));
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (Exception)
        {
            // shutdown
        }

        ServerCertificate.Dispose();
        CaCertificate.Dispose();
        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // stop
        }
        catch (ObjectDisposedException)
        {
            // stop
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            await using SslStream ssl = new(client.GetStream(), leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions
                {
                    ServerCertificate = ServerCertificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                },
                ct).ConfigureAwait(false);

            PipeReader reader = PipeReader.Create(ssl);
            PipeWriter writer = PipeWriter.Create(ssl);
            using ApiSentenceParser parser = new();

            while (!ct.IsCancellationRequested)
            {
                ReadResult read = await reader.ReadAsync(ct).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = read.Buffer;
                try
                {
                    while (buffer.Length > 0)
                    {
                        ApiSentenceParseStatus status = parser.TryRead(ref buffer, out RosSentenceLease? lease, out _);
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
                            string head = Encoding.ASCII.GetString(lease.Sentence.Head!.Value.Payload.Span);
                            if (head == "/login")
                            {
                                Interlocked.Increment(ref _loginAttempts);
                                await WriteLoginResponseAsync(writer).ConfigureAwait(false);
                                // Keep TLS session open so the client can take over RosSession.
                                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                                return;
                            }
                        }
                    }

                    if (read.IsCompleted)
                    {
                        return;
                    }
                }
                finally
                {
                    reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
    }

    private async Task WriteLoginResponseAsync(PipeWriter writer)
    {
        ArrayBufferWriter<byte> buffer = new();
        switch (_behavior)
        {
            case LoginBehavior.Accept:
                ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[]
                {
                    "!done"u8.ToArray(),
                });
                break;
            case LoginBehavior.Reject:
                ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[]
                {
                    "!trap"u8.ToArray(),
                    "=message=cannot log in"u8.ToArray(),
                });
                ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[]
                {
                    "!done"u8.ToArray(),
                });
                break;
            case LoginBehavior.LegacyChallenge:
                ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[]
                {
                    "!done"u8.ToArray(),
                    "=ret=00112233445566778899aabbccddeeff"u8.ToArray(),
                });
                break;
        }

        await writer.WriteAsync(buffer.WrittenMemory).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static (X509Certificate2 Ca, X509Certificate2 Server) CreateCerts(
        CertificateValidity validity,
        string sanHost)
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest caRequest = new(
            "CN=Mfc Test CA",
            caKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        X509Certificate2 ca = caRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-60),
            DateTimeOffset.UtcNow.AddYears(2));

        using RSA serverKey = RSA.Create(2048);
        CertificateRequest serverRequest = new(
            $"CN={sanHost}",
            serverKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        serverRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        serverRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")],
                false));

        SubjectAlternativeNameBuilder san = new();
        if (IPAddress.TryParse(sanHost, out IPAddress? ip))
        {
            san.AddIpAddress(ip);
        }
        else
        {
            san.AddDnsName(sanHost);
        }

        serverRequest.CertificateExtensions.Add(san.Build());

        DateTimeOffset notBefore;
        DateTimeOffset notAfter;
        if (validity == CertificateValidity.Expired)
        {
            notBefore = DateTimeOffset.UtcNow.AddDays(-30);
            notAfter = DateTimeOffset.UtcNow.AddDays(-1);
        }
        else
        {
            notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            notAfter = DateTimeOffset.UtcNow.AddDays(30);
        }

        using X509Certificate2 serverEphemeral = serverRequest.Create(
            ca,
            notBefore,
            notAfter,
            serialNumber: RandomNumberGenerator.GetBytes(8));
        X509Certificate2 server = serverEphemeral.CopyWithPrivateKey(serverKey);

        // Export/import so the cert has a durable private key for SslStream on all platforms.
        return (
            X509CertificateLoader.LoadPkcs12(ca.Export(X509ContentType.Pfx), password: null),
            X509CertificateLoader.LoadPkcs12(server.Export(X509ContentType.Pfx), password: null));
    }
}
