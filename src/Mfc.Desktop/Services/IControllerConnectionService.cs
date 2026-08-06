namespace Mfc.Desktop.Services;

/// <summary>Connects to Controller health endpoint off the UI thread.</summary>
public interface IControllerConnectionService : IAsyncDisposable
{
    ControllerConnectionState State { get; }

    string? LastError { get; }

    event EventHandler? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
