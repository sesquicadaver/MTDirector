using Grpc.Net.Client;

namespace Mfc.Desktop.Services;

/// <summary>Connects to Controller health endpoint off the UI thread.</summary>
public interface IControllerConnectionService : IAsyncDisposable
{
    ControllerConnectionState State { get; }

    string? LastError { get; }

    /// <summary>Active gRPC channel when <see cref="State"/> is Connected; otherwise null.</summary>
    GrpcChannel? Channel { get; }

    event EventHandler? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
