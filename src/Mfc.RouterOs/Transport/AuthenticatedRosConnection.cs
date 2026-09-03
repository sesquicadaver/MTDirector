using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Transport;

/// <summary>
/// Owns a verified API-SSL transport and an authenticated <see cref="RosSession"/>.
/// Returned only after successful login; no reconnect loop (Spec §14–15).
/// </summary>
public sealed class AuthenticatedRosConnection : IAsyncDisposable
{
    private readonly TcpClient _tcp;
    private readonly SslStream _ssl;
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private bool _disposed;

    private AuthenticatedRosConnection(
        TcpClient tcp,
        SslStream ssl,
        PipeReader reader,
        PipeWriter writer,
        RosSession session,
        X509Certificate2 remoteCertificate,
        SslProtocols negotiatedProtocol)
    {
        _tcp = tcp;
        _ssl = ssl;
        _reader = reader;
        _writer = writer;
        Session = session;
        RemoteCertificate = remoteCertificate;
        NegotiatedProtocol = negotiatedProtocol;
    }

    public RosSession Session { get; }

    public X509Certificate2 RemoteCertificate { get; }

    public SslProtocols NegotiatedProtocol { get; }

    /// <summary>
    /// Connects via API-SSL only, validates the certificate, authenticates once, then returns a session.
    /// </summary>
    public static async Task<AuthenticatedRosConnection> ConnectAsync(
        ApiSslConnectOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        using CancellationTokenSource timeoutCts = new(options.TlsAndLoginTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        TcpClient? tcp = null;
        SslStream? ssl = null;
        try
        {
            tcp = new TcpClient();
            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            connectCts.CancelAfter(options.ConnectTimeout);
            try
            {
                await tcp.ConnectAsync(options.Host, options.Port, connectCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (connectCts.IsCancellationRequested
                                                     && !cancellationToken.IsCancellationRequested)
            {
                throw new ApiSslException(ApiSslErrors.HandshakeFailed, "TCP connect timed out.");
            }

            NetworkStream network = tcp.GetStream();
            ApiSslException? validationError = null;
            ssl = new SslStream(
                network,
                leaveInnerStreamOpen: false,
                (_, certificate, chain, errors) =>
                {
                    X509Certificate2? cert2 = certificate switch
                    {
                        X509Certificate2 c2 => c2,
                        not null => new X509Certificate2(certificate),
                        _ => null,
                    };
                    return ApiSslCertificateValidator.Validate(
                        cert2,
                        chain,
                        errors,
                        options,
                        out validationError);
                });

            // Custom RemoteCertificateValidationCallback owns trust/revocation policy.
            // Keep SslStream OS revocation off so INTERNAL_CA CustomRootTrust + options.CertificateRevocationMode
            // are the single source of truth (SEC-04).
            SslClientAuthenticationOptions sslOptions = new()
            {
                TargetHost = options.Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
            };

            try
            {
                await ssl.AuthenticateAsClientAsync(sslOptions, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested
                                                     && !cancellationToken.IsCancellationRequested)
            {
                throw new ApiSslException(ApiSslErrors.AuthenticationTimeout, "TLS/login timed out.");
            }
            catch (AuthenticationException ex)
            {
                throw validationError
                      ?? new ApiSslException(ApiSslErrors.HandshakeFailed, "TLS handshake failed.", ex);
            }

            if (!ssl.IsAuthenticated || ssl.RemoteCertificate is null)
            {
                throw validationError
                      ?? new ApiSslException(ApiSslErrors.HandshakeFailed, "TLS authentication incomplete.");
            }

            X509Certificate2 remote = new(ssl.RemoteCertificate);
            PipeReader reader = PipeReader.Create(ssl);
            PipeWriter writer = PipeWriter.Create(ssl);

            try
            {
                await RouterOsLogin.AuthenticateAsync(
                    reader,
                    writer,
                    options.Username,
                    options.Password,
                    linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested
                                                     && !cancellationToken.IsCancellationRequested)
            {
                throw new ApiSslException(ApiSslErrors.AuthenticationTimeout, "TLS/login timed out.");
            }

            RosSession session = new(reader, writer, options.SessionOptions);
            AuthenticatedRosConnection connection = new(
                tcp,
                ssl,
                reader,
                writer,
                session,
                remote,
                ssl.SslProtocol);
            tcp = null;
            ssl = null;
            return connection;
        }
        finally
        {
            options.Password.Dispose();
            if (ssl is not null)
            {
                await ssl.DisposeAsync().ConfigureAwait(false);
            }

            tcp?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await Session.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Idempotent.
        }

        try
        {
            await _writer.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Idempotent.
        }

        await _ssl.DisposeAsync().ConfigureAwait(false);
        _tcp.Dispose();
        RemoteCertificate.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void ValidateOptions(ApiSslConnectOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Username);
        ArgumentNullException.ThrowIfNull(options.Password);

        // Plain (non-TLS) transport does not exist in this assembly; any port still requires SslStream.
        if (options.Port == 8728)
        {
            throw new ApiSslException(
                ApiSslErrors.PlainApiForbidden,
                "Plain RouterOS API port 8728 is forbidden.");
        }
    }
}
