using Mfc.Application.Abstractions.RouterOs;

namespace Mfc.RouterOs.Ports;

/// <summary>
/// Production default <see cref="ISnapshotCapturePort"/> when no live capture adapter is registered.
/// CaptureSnapshotUseCase maps the failure to a typed application error.
/// Integration tests replace this via <c>Program.BuildHost(..., configure)</c>.
/// </summary>
public sealed class NotConfiguredSnapshotCapturePort : ISnapshotCapturePort
{
    public const string NotConfiguredMessage =
        "Snapshot capture port is not_configured for live RouterOS capture; inject a capture adapter for StartCapture.";

    /// <inheritdoc />
    public Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(NotConfiguredMessage);
    }
}
