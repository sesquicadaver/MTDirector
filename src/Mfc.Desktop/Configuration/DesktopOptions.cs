namespace Mfc.Desktop.Configuration;

/// <summary>Desktop-side controller endpoint and health-check settings.</summary>
public sealed class DesktopOptions
{
    public const string SectionName = "Desktop";

    /// <summary>Controller gRPC base address, e.g. https://127.0.0.1:5101</summary>
    public string ControllerEndpoint { get; init; } = "https://127.0.0.1:5101";

    /// <summary>Per-attempt health check timeout.</summary>
    public int HealthCheckTimeoutSeconds { get; init; } = 5;

    /// <summary>Maximum automatic reconnect attempts after a drop.</summary>
    public int MaxReconnectAttempts { get; init; } = 3;

    /// <summary>Delay between reconnect attempts.</summary>
    public int ReconnectDelayMilliseconds { get; init; } = 1000;
}
