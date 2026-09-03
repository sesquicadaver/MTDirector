using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>
/// Bounded gRPC health client. Network I/O never runs on the Avalonia UI thread.
/// Does not store or transmit RouterOS credentials.
/// </summary>
public sealed class ControllerConnectionService : IControllerConnectionService
{
    private readonly DesktopOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GrpcChannel? _channel;
    private CancellationTokenSource? _reconnectCts;
    private int _reconnectLoopRunning;
    private ControllerConnectionState _state = ControllerConnectionState.Disconnected;
    private string? _lastError;

    public ControllerConnectionService(DesktopOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ControllerConnectionState State => _state;

    public string? LastError => _lastError;

    /// <inheritdoc />
    public GrpcChannel? Channel =>
        _state == ControllerConnectionState.Connected ? _channel : null;

    public event EventHandler? StateChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopReconnectLoop_NoLock();
            await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
            StartReconnectLoop_NoLock();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            StopReconnectLoop_NoLock();
            await DisposeChannelAsync().ConfigureAwait(false);
            SetState(ControllerConnectionState.Disconnected, error: null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        SetState(ControllerConnectionState.Connecting, error: null);

        await DisposeChannelAsync().ConfigureAwait(false);

        try
        {
            Uri endpoint = new(_options.ControllerEndpoint);
            if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            SocketsHttpHandler httpHandler = DesktopGrpcHttpHandlerFactory.Create(_options);

            _channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
            {
                HttpHandler = httpHandler,
                DisposeHttpClient = true,
            });

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds));

            Health.HealthClient client = new(_channel);
            HealthCheckResponse response = await client.CheckAsync(
                    new HealthCheckRequest(),
                    cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);

            if (response.Status != HealthCheckResponse.Types.ServingStatus.Serving)
            {
                SetState(ControllerConnectionState.Disconnected, $"Health status: {response.Status}");
                await DisposeChannelAsync().ConfigureAwait(false);
                return;
            }

            SetState(ControllerConnectionState.Connected, error: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SetState(ControllerConnectionState.Disconnected, "Health check timed out.");
            await DisposeChannelAsync().ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated || ex.StatusCode == StatusCode.PermissionDenied)
        {
            SetState(ControllerConnectionState.AuthenticationFailed, ex.Status.Detail);
            await DisposeChannelAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsTlsFailure(ex))
        {
            SetState(ControllerConnectionState.TlsError, ex.Message);
            await DisposeChannelAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetState(ControllerConnectionState.Disconnected, ex.Message);
            await DisposeChannelAsync().ConfigureAwait(false);
        }
    }

    private void StartReconnectLoop_NoLock()
    {
        if (Interlocked.CompareExchange(ref _reconnectLoopRunning, 1, 0) != 0)
        {
            return;
        }

        _reconnectCts = new CancellationTokenSource();
        CancellationToken token = _reconnectCts.Token;
        _ = RunReconnectLoopAsync(token);
    }

    private void StopReconnectLoop_NoLock()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        Interlocked.Exchange(ref _reconnectLoopRunning, 0);
    }

    private async Task RunReconnectLoopAsync(CancellationToken token)
    {
        try
        {
            int attempts = 0;
            while (!token.IsCancellationRequested && attempts < _options.MaxReconnectAttempts)
            {
                if (_state == ControllerConnectionState.Connected)
                {
                    await Task.Delay(_options.ReconnectDelayMilliseconds, token).ConfigureAwait(false);
                    continue;
                }

                if (_state is ControllerConnectionState.AuthenticationFailed or ControllerConnectionState.TlsError)
                {
                    break;
                }

                attempts++;
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (_state == ControllerConnectionState.Connected)
                    {
                        continue;
                    }

                    await ConnectCoreAsync(token).ConfigureAwait(false);
                }
                finally
                {
                    _gate.Release();
                }

                if (_state != ControllerConnectionState.Connected)
                {
                    await Task.Delay(_options.ReconnectDelayMilliseconds, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        finally
        {
            Interlocked.Exchange(ref _reconnectLoopRunning, 0);
        }
    }

    private async Task DisposeChannelAsync()
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            await _channel.ShutdownAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore shutdown races
        }

        _channel.Dispose();
        _channel = null;
    }

    private void SetState(ControllerConnectionState state, string? error)
    {
        _state = state;
        _lastError = error;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsTlsFailure(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is AuthenticationException or HttpRequestException { InnerException: AuthenticationException })
            {
                return true;
            }

            string message = current.Message;
            if (message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
                || message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
                || message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
