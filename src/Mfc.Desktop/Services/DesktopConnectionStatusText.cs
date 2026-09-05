using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>
/// Formats shell connection status for the Desktop chrome (W7-08).
/// When Connected, includes the same actor string sent as <c>x-mfc-actor</c>.
/// </summary>
public static class DesktopConnectionStatusText
{
    /// <summary>Builds the operator-visible status line for the current connection state.</summary>
    public static string Format(ControllerConnectionState state, DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string baseStatus = state switch
        {
            ControllerConnectionState.Connecting => "Connecting",
            ControllerConnectionState.Connected => "Connected",
            ControllerConnectionState.Disconnected => "Disconnected",
            ControllerConnectionState.AuthenticationFailed => "AuthenticationFailed",
            ControllerConnectionState.TlsError => "TlsError",
            _ => state.ToString(),
        };

        if (state != ControllerConnectionState.Connected)
        {
            return baseStatus;
        }

        string actor = DesktopGrpcActorResolver.Resolve(options);
        return $"{baseStatus} · actor: {actor}";
    }
}
