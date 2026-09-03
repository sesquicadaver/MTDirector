namespace Mfc.Desktop.Configuration;

/// <summary>Desktop-side controller endpoint and health-check settings.</summary>
public sealed class DesktopOptions
{
    public const string SectionName = "Desktop";

    /// <summary>Controller gRPC base address, e.g. https://127.0.0.1:5101</summary>
    public string ControllerEndpoint { get; init; } = "https://127.0.0.1:5101";

    /// <summary>Actor identity sent as <c>x-mfc-actor</c> on inventory RPCs.</summary>
    public string Actor { get; init; } = "desktop";

    /// <summary>Per-attempt health check timeout.</summary>
    public int HealthCheckTimeoutSeconds { get; init; } = 5;

    /// <summary>Maximum automatic reconnect attempts after a drop.</summary>
    public int MaxReconnectAttempts { get; init; } = 3;

    /// <summary>Delay between reconnect attempts.</summary>
    public int ReconnectDelayMilliseconds { get; init; } = 1000;

    /// <summary>
    /// Optional path to a client certificate (PFX) presented to Controller when mTLS is enabled (W7-03).
    /// Empty keeps the previous no-client-cert behaviour (Development HTTP loopback).
    /// </summary>
    public string? ClientCertificatePath { get; init; }

    /// <summary>Optional password for <see cref="ClientCertificatePath"/> PFX.</summary>
    public string? ClientCertificatePassword { get; init; }
}
