namespace Mfc.Desktop.Services;

/// <summary>Visible controller connection states for the Desktop shell.</summary>
public enum ControllerConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    AuthenticationFailed = 3,
    TlsError = 4,
}
