using Mfc.Application.Abstractions.RouterOs;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Application port adapter for stable-read coordination (M1-19).</summary>
public sealed class RouterOsStableReadCoordinatorPort : IStableReadCoordinatorPort
{
    private readonly IRouterOsStableReadAttemptFactoryProvider _factoryProvider;
    private readonly StableReadCoordinator _coordinator;

    public RouterOsStableReadCoordinatorPort(
        IRouterOsStableReadAttemptFactoryProvider factoryProvider,
        StableReadCoordinator? coordinator = null)
    {
        ArgumentNullException.ThrowIfNull(factoryProvider);
        _factoryProvider = factoryProvider;
        _coordinator = coordinator ?? new StableReadCoordinator();
    }

    public async Task<StableReadCoordinationResult> CoordinateAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IStableReadAttemptFactory<RouterOsDiscoveryDataset> factory = _factoryProvider.Create(target);
        StableReadResult<RouterOsDiscoveryDataset> result = await _coordinator
            .ExecuteAsync(factory, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string outcome = result.Outcome switch
        {
            StableReadOutcome.Accepted => StableReadOutcomeCodes.Accepted,
            StableReadOutcome.SnapshotUnstable => StableReadOutcomeCodes.SnapshotUnstable,
            StableReadOutcome.Canceled => StableReadOutcomeCodes.Canceled,
            _ => StableReadOutcomeCodes.SnapshotUnstable,
        };

        IReadOnlyDictionary<string, string>? digests = null;
        if (result.AcceptedFingerprints is not null)
        {
            digests = result.AcceptedFingerprints.Menus.ToDictionary(
                static m => m.Menu.ToString(),
                static m => m.Digest.ToString(),
                StringComparer.Ordinal);
        }

        return new StableReadCoordinationResult
        {
            Outcome = outcome,
            AttemptsUsed = result.AttemptsUsed,
            ConfigurationFingerprintHex = result.AcceptedFingerprints?.AggregateDigest.ToString(),
            DiscoverySectionDigests = digests,
        };
    }
}
