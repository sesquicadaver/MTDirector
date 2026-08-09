using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Snapshot;
using Xunit;

namespace Mfc.UnitTests.RouterOs.FaultInjection;

/// <summary>M1-33 unstable-configuration fault: SNAPSHOT_UNSTABLE without a complete dataset.</summary>
public sealed class StableReadFaultInjectionTests
{
    [Fact]
    public async Task UnstableConfigurationYieldsSnapshotUnstableWithoutCompleteDataset()
    {
        StableReadCoordinator coordinator = new(new NoDelay());
        DriftingAttemptFactory factory = new(seeds: [1, 2, 3]);

        StableReadResult<string> result = await coordinator.ExecuteAsync(
            factory,
            new StableReadOptions
            {
                MaxAttempts = 3,
                RetryDelayMin = TimeSpan.Zero,
                RetryDelayMax = TimeSpan.Zero,
            },
            CancellationToken.None);

        Assert.Equal(StableReadOutcome.SnapshotUnstable, result.Outcome);
        Assert.Equal("SNAPSHOT_UNSTABLE", StableReadOutcomeCodes.SnapshotUnstable);
        Assert.False(result.IsComplete);
        Assert.Null(result.Dataset);
        Assert.True(result.AttemptsUsed <= 3);
    }

    [Fact]
    public async Task RetryCountDoesNotExceedConfiguredMaxAttempts()
    {
        ConcurrentBag<int> opens = [];
        StableReadCoordinator coordinator = new(new NoDelay());
        CountingDriftFactory factory = new(opens);

        StableReadResult<string> result = await coordinator.ExecuteAsync(
            factory,
            new StableReadOptions
            {
                MaxAttempts = 2,
                RetryDelayMin = TimeSpan.Zero,
                RetryDelayMax = TimeSpan.Zero,
            },
            CancellationToken.None);

        Assert.Equal(StableReadOutcome.SnapshotUnstable, result.Outcome);
        Assert.Equal(2, opens.Count);
        Assert.Equal(2, result.AttemptsUsed);
    }

    private sealed class NoDelay : IStableReadDelay
    {
        public Task DelayAsync(TimeSpan min, TimeSpan max, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class DriftingAttemptFactory : IStableReadAttemptFactory<string>
    {
        private readonly Queue<int> _seeds;

        public DriftingAttemptFactory(IEnumerable<int> seeds) => _seeds = new Queue<int>(seeds);

        public Task<IStableReadAttemptSession<string>> OpenAsync(CancellationToken cancellationToken)
        {
            int seed = _seeds.Dequeue();
            return Task.FromResult<IStableReadAttemptSession<string>>(new DriftSession(seed));
        }
    }

    private sealed class CountingDriftFactory : IStableReadAttemptFactory<string>
    {
        private readonly ConcurrentBag<int> _opens;
        private int _seed;

        public CountingDriftFactory(ConcurrentBag<int> opens) => _opens = opens;

        public Task<IStableReadAttemptSession<string>> OpenAsync(CancellationToken cancellationToken)
        {
            int seed = Interlocked.Increment(ref _seed);
            _opens.Add(seed);
            return Task.FromResult<IStableReadAttemptSession<string>>(new DriftSession(seed));
        }
    }

    private sealed class DriftSession : IStableReadAttemptSession<string>
    {
        private readonly int _before;
        private int _pass;

        public DriftSession(int before) => _before = before;

        public Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
        {
            int seed = _pass++ == 0 ? _before : _before + 100;
            return Task.FromResult(FingerprintFromSeed(seed));
        }

        public Task<string> ReadCompleteDiscoveryDatasetAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
            => Task.FromResult("discarded");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static ConfigurationFingerprintSet FingerprintFromSeed(int seed)
        {
            byte[] digest = new byte[Hash256.Size];
            byte[] seedBytes = Encoding.UTF8.GetBytes(seed.ToString(CultureInfo.InvariantCulture));
            Array.Copy(seedBytes, digest, Math.Min(seedBytes.Length, digest.Length));
            Hash256 hash = Hash256.Create(digest);
            List<MenuFingerprint> menus = [];
            foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
            {
                menus.Add(new MenuFingerprint
                {
                    Menu = menu,
                    Digest = hash,
                    Available = menu != CriticalConfigurationMenu.ManagedAnchors,
                });
            }

            return new ConfigurationFingerprintSet(menus);
        }
    }
}
