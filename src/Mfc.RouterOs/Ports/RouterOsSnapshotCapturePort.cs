using Mfc.Application.Abstractions.RouterOs;
using Mfc.RouterOs.Snapshot;

namespace Mfc.RouterOs.Ports;

/// <summary>
/// Production <see cref="ISnapshotCapturePort"/> — stable-read + canonical/raw snapshot assembly (P2-05 / M1-19…M1-22).
/// </summary>
public sealed class RouterOsSnapshotCapturePort : ISnapshotCapturePort
{
    private readonly IRouterOsStableReadAttemptFactoryProvider _factoryProvider;
    private readonly StableReadCoordinator _coordinator;

    public RouterOsSnapshotCapturePort(
        IRouterOsStableReadAttemptFactoryProvider factoryProvider,
        StableReadCoordinator? coordinator = null)
    {
        ArgumentNullException.ThrowIfNull(factoryProvider);
        _factoryProvider = factoryProvider;
        _coordinator = coordinator ?? new StableReadCoordinator();
    }

    /// <inheritdoc />
    public async Task<SnapshotCaptureResult> CaptureAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        IStableReadAttemptFactory<RouterOsDiscoveryDataset> factory = _factoryProvider.Create(target);
        StableReadResult<RouterOsDiscoveryDataset> stable = await _coordinator
            .ExecuteAsync(factory, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (stable.Outcome == StableReadOutcome.SnapshotUnstable)
        {
            throw new InvalidOperationException(
                "SNAPSHOT_UNSTABLE: configuration changed across bounded stable-read attempts.");
        }

        if (stable.Outcome == StableReadOutcome.Canceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("SNAPSHOT_UNSTABLE: stable-read was canceled.");
        }

        if (!stable.IsComplete || stable.Dataset is null)
        {
            throw new InvalidOperationException("SNAPSHOT_UNSTABLE: stable-read did not produce a complete dataset.");
        }

        try
        {
            return SnapshotCaptureResultBuilder.Build(stable.Dataset);
        }
        catch (RawSnapshotTooLargeException ex)
        {
            throw new InvalidOperationException($"SNAPSHOT_TOO_LARGE: {ex.Message}", ex);
        }
    }
}
