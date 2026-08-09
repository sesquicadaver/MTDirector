namespace Mfc.RouterOs.Snapshot;

/// <summary>
/// Coordinates stable-read snapshot attempts (M1-19 / MVP §8.2).
/// Algorithm: fingerprints → complete discovery → fingerprints → accept or bounded retry.
/// Never issues RouterOS write commands; never marks unstable results as complete.
/// </summary>
public sealed class StableReadCoordinator
{
    public const string SnapshotUnstableCode = "SNAPSHOT_UNSTABLE";

    private readonly IStableReadDelay _delay;

    public StableReadCoordinator(IStableReadDelay? delay = null)
    {
        _delay = delay ?? new JitterStableReadDelay();
    }

    /// <summary>
    /// Runs up to <see cref="StableReadOptions.MaxAttempts"/> attempts.
    /// Each attempt opens a fresh session; data from failed attempts is discarded.
    /// </summary>
    public async Task<StableReadResult<TDataset>> ExecuteAsync<TDataset>(
        IStableReadAttemptFactory<TDataset> attemptFactory,
        StableReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attemptFactory);
        StableReadOptions effective = options ?? new StableReadOptions();
        effective.Validate();

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(effective.FullCaptureTimeout);
        CancellationToken ct = linked.Token;

        int attemptsUsed = 0;
        for (int attempt = 1; attempt <= effective.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            attemptsUsed = attempt;

            await using IStableReadAttemptSession<TDataset> session =
                await attemptFactory.OpenAsync(ct).ConfigureAwait(false);

            await using BoundedCommandParallelism parallelism = new(effective.MaxParallelCommands);
            StableReadExecutionContext context = new(effective, parallelism);

            ConfigurationFingerprintSet before;
            TDataset dataset;
            ConfigurationFingerprintSet after;
            try
            {
                before = await session.ReadConfigurationFingerprintsAsync(context, ct).ConfigureAwait(false);
                dataset = await session.ReadCompleteDiscoveryDatasetAsync(context, ct).ConfigureAwait(false);
                after = await session.ReadConfigurationFingerprintsAsync(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return UnstableOrCanceled<TDataset>(StableReadOutcome.Canceled, attemptsUsed);
            }

            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);
            ArgumentNullException.ThrowIfNull(dataset);

            if (before.Equals(after))
            {
                return new StableReadResult<TDataset>
                {
                    Outcome = StableReadOutcome.Accepted,
                    Dataset = dataset,
                    AcceptedFingerprints = after,
                    AttemptsUsed = attemptsUsed,
                };
            }

            // Discard attempt data (dataset goes out of scope). Retry with bounded delay.
            if (attempt < effective.MaxAttempts)
            {
                try
                {
                    await _delay.DelayAsync(effective.RetryDelayMin, effective.RetryDelayMax, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return UnstableOrCanceled<TDataset>(StableReadOutcome.Canceled, attemptsUsed);
                }
            }
        }

        return new StableReadResult<TDataset>
        {
            Outcome = StableReadOutcome.SnapshotUnstable,
            Dataset = default,
            AcceptedFingerprints = null,
            AttemptsUsed = attemptsUsed,
        };
    }

    private static StableReadResult<TDataset> UnstableOrCanceled<TDataset>(
        StableReadOutcome outcome,
        int attemptsUsed)
        => new()
        {
            Outcome = outcome,
            Dataset = default,
            AcceptedFingerprints = null,
            AttemptsUsed = attemptsUsed,
        };
}
