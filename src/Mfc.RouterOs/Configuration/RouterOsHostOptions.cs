using System.ComponentModel.DataAnnotations;

namespace Mfc.RouterOs.Configuration;

/// <summary>Controller configuration for production RouterOS read/write wiring (P2-06 / P2-10).</summary>
public sealed class RouterOsHostOptions
{
    public const string SectionName = "RouterOs";

    /// <summary>
    /// When <see langword="true"/>, registers production read/capture ports.
    /// Fail-closed default is <see langword="false"/> (CI and Development unless explicitly set).
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// When <see langword="true"/>, registers production write-path ports (onboarding / deploy / watchdog residue).
    /// Fail-closed default is <see langword="false"/> until an operator opts in (P2-10).
    /// Independent of <see cref="Enabled"/> (read path).
    /// </summary>
    public bool WriteEnabled { get; init; }

    /// <summary>Bounded API-SSL probe timeout for inventory validation (seconds).</summary>
    [Range(1, 600)]
    public int ProbeTimeoutSeconds { get; init; } = 30;
}
