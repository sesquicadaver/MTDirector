using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Snapshot;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class StableReadCoordinatorTests
{
    [Fact]
    public async Task AcceptsWhenFingerprintsMatchAcrossDiscovery()
    {
        RecordingDelay delay = new();
        StableReadCoordinator coordinator = new(delay);
        ScriptedAttemptFactory factory = new(
            [
                AttemptScript.Stable("dataset-1", fingerprintSeed: 1),
            ]);

        StableReadResult<string> result = await coordinator.ExecuteAsync(factory, new StableReadOptions
        {
            MaxAttempts = 3,
            RetryDelayMin = TimeSpan.FromMilliseconds(1),
            RetryDelayMax = TimeSpan.FromMilliseconds(2),
        });

        Assert.Equal(StableReadOutcome.Accepted, result.Outcome);
        Assert.True(result.IsComplete);
        Assert.Equal("dataset-1", result.Dataset);
        Assert.Equal(1, result.AttemptsUsed);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task RetriesWithBoundedDelayThenReturnsSnapshotUnstable()
    {
        RecordingDelay delay = new();
        StableReadCoordinator coordinator = new(delay);
        ScriptedAttemptFactory factory = new(
            [
                AttemptScript.Drifting(fingerprintBefore: 1, fingerprintAfter: 2),
                AttemptScript.Drifting(fingerprintBefore: 3, fingerprintAfter: 4),
                AttemptScript.Drifting(fingerprintBefore: 5, fingerprintAfter: 6),
            ]);

        StableReadResult<string> result = await coordinator.ExecuteAsync(factory, new StableReadOptions
        {
            MaxAttempts = 3,
            RetryDelayMin = TimeSpan.FromMilliseconds(10),
            RetryDelayMax = TimeSpan.FromMilliseconds(20),
        });

        Assert.Equal(StableReadOutcome.SnapshotUnstable, result.Outcome);
        Assert.False(result.IsComplete);
        Assert.Null(result.Dataset);
        Assert.Equal(3, result.AttemptsUsed);
        Assert.Equal(2, delay.Delays.Count);
        Assert.All(delay.Delays, d =>
        {
            Assert.True(d >= TimeSpan.FromMilliseconds(10));
            Assert.True(d <= TimeSpan.FromMilliseconds(20));
        });
    }

    [Fact]
    public async Task DetectsConcurrentConfigurationChangeThenAcceptsOnStableRetry()
    {
        // Simulates CHR-style concurrent config change on first attempt (AC#10).
        StableReadCoordinator coordinator = new(new RecordingDelay());
        ScriptedAttemptFactory factory = new(
            [
                AttemptScript.Drifting(fingerprintBefore: 10, fingerprintAfter: 11),
                AttemptScript.Stable("after-change", fingerprintSeed: 20),
            ]);

        StableReadResult<string> result = await coordinator.ExecuteAsync(factory, new StableReadOptions
        {
            MaxAttempts = 3,
            RetryDelayMin = TimeSpan.Zero,
            RetryDelayMax = TimeSpan.Zero,
        });

        Assert.Equal(StableReadOutcome.Accepted, result.Outcome);
        Assert.Equal("after-change", result.Dataset);
        Assert.Equal(2, result.AttemptsUsed);
    }

    [Fact]
    public async Task CancellationStopsEntireSnapshot()
    {
        StableReadCoordinator coordinator = new(new RecordingDelay());
        using CancellationTokenSource cts = new();
        ScriptedAttemptFactory factory = new(
            [
                AttemptScript.Stable("never", fingerprintSeed: 1, onFingerprint: () => cts.Cancel()),
            ]);

        StableReadResult<string> result = await coordinator.ExecuteAsync(
            factory,
            new StableReadOptions { MaxAttempts = 3 },
            cts.Token);

        Assert.Equal(StableReadOutcome.Canceled, result.Outcome);
        Assert.False(result.IsComplete);
        Assert.Null(result.Dataset);
    }

    [Fact]
    public async Task FullCaptureTimeoutCancelsCoordination()
    {
        StableReadCoordinator coordinator = new(new RecordingDelay());
        ScriptedAttemptFactory factory = new(
            [
                AttemptScript.HangUntilCanceled("slow", fingerprintSeed: 1),
            ]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await coordinator.ExecuteAsync(
                factory,
                new StableReadOptions
                {
                    MaxAttempts = 3,
                    FullCaptureTimeout = TimeSpan.FromMilliseconds(50),
                    CommandTimeout = TimeSpan.FromSeconds(30),
                }));
    }

    [Fact]
    public void CriticalMenusExcludeObservationOnlyCommands()
    {
        foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
        {
            foreach (RosReadCommandId id in CriticalConfigurationMenus.CommandsFor(menu))
            {
                Assert.False(
                    ConfigurationFingerprintBuilder.IsObservationOnlyCommand(id),
                    $"Menu {menu} must not fingerprint observation-only command {id}.");
            }
        }

        Assert.Contains(RosReadCommandId.Ipv4Filter, CriticalConfigurationMenus.CommandsFor(CriticalConfigurationMenu.Filter));
        Assert.Contains(RosReadCommandId.VrrpInterfaces, CriticalConfigurationMenus.CommandsFor(CriticalConfigurationMenu.Vrrp));
        Assert.Contains(RosReadCommandId.IpServices, CriticalConfigurationMenus.CommandsFor(CriticalConfigurationMenu.IpServices));
    }

    [Fact]
    public void FingerprintIgnoresObservationProperties()
    {
        RosReadCommandResult withUptime = new()
        {
            CommandId = RosReadCommandId.IpServices,
            Lifecycle = RosCommandLifecycle.Completed,
            SessionInvalidated = false,
            Error = null,
            Records =
            [
                new RosReadRecord
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "api-ssl",
                        ["port"] = "8729",
                        ["disabled"] = "false",
                        ["dynamic"] = "false",
                        ["invalid"] = "false",
                    },
                    RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
        };

        RosReadCommandResult observationChanged = new()
        {
            CommandId = RosReadCommandId.IpServices,
            Lifecycle = RosCommandLifecycle.Completed,
            SessionInvalidated = false,
            Error = null,
            Records =
            [
                new RosReadRecord
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "api-ssl",
                        ["port"] = "8729",
                        ["disabled"] = "false",
                        ["dynamic"] = "true",
                        ["invalid"] = "true",
                    },
                    RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
        };

        Hash256 a = ConfigurationFingerprintBuilder.DigestCommandConfiguration(withUptime);
        Hash256 b = ConfigurationFingerprintBuilder.DigestCommandConfiguration(observationChanged);
        Assert.Equal(a, b);

        RosReadCommandResult configChanged = new()
        {
            CommandId = RosReadCommandId.IpServices,
            Lifecycle = RosCommandLifecycle.Completed,
            SessionInvalidated = false,
            Error = null,
            Records =
            [
                new RosReadRecord
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "api-ssl",
                        ["port"] = "8730",
                        ["disabled"] = "false",
                    },
                    RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
        };

        Assert.NotEqual(a, ConfigurationFingerprintBuilder.DigestCommandConfiguration(configChanged));
    }

    [Fact]
    public async Task ParallelReadsHonorBoundedConcurrency()
    {
        await using BoundedCommandParallelism gate = new(maxParallelCommands: 2);
        int concurrent = 0;
        int peak = 0;
        object sync = new();

        List<Func<CancellationToken, Task<int>>> actions = [];
        for (int i = 0; i < 6; i++)
        {
            actions.Add(async ct =>
            {
                int now;
                lock (sync)
                {
                    concurrent++;
                    peak = Math.Max(peak, concurrent);
                    now = concurrent;
                }

                await Task.Delay(30, ct).ConfigureAwait(false);
                lock (sync)
                {
                    concurrent--;
                }

                return now;
            });
        }

        _ = await gate.RunAllAsync(actions, CancellationToken.None);
        Assert.True(peak <= 2, $"Peak concurrency {peak} exceeded bound 2.");
    }

    [Fact]
    public void OptionsRejectUnboundedLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StableReadOptions { MaxAttempts = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StableReadOptions { MaxParallelCommands = 9 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StableReadOptions { CommandTimeout = TimeSpan.Zero }.Validate());
    }

    [Fact]
    public void CoordinatorSourceContainsNoWriteCommands()
    {
        string? dir = Path.GetDirectoryName(typeof(StableReadCoordinator).Assembly.Location);
        Assert.NotNull(dir);
        // Source files are not in output; inspect public surface + type names instead.
        Type coordinatorType = typeof(StableReadCoordinator);
        Assert.DoesNotContain("Write", coordinatorType.FullName, StringComparison.OrdinalIgnoreCase);

        foreach (Type type in coordinatorType.Assembly.GetTypes()
                     .Where(t => t.Namespace is not null
                                 && t.Namespace.StartsWith("Mfc.RouterOs.Snapshot", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(
                "Mfc.RouterOs.Write",
                type.FullName,
                StringComparison.Ordinal);
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.DoesNotContain("Write", method.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Fingerprint command set is read-only allowlist paths.
        foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
        {
            foreach (RosReadCommandId id in CriticalConfigurationMenus.CommandsFor(menu))
            {
                RosReadCommandDefinition def = RosReadCommandRegistry.Get(id);
                Assert.StartsWith("/", def.FixedPath, StringComparison.Ordinal);
                Assert.EndsWith("/print", def.FixedPath, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void CommandTimeoutDefaultIsThirtySeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), StableReadOptions.DefaultCommandTimeout);
        Assert.Equal(8, StableReadOptions.DefaultMaxParallelCommands);
        Assert.Equal(3, StableReadOptions.DefaultMaxAttempts);
    }

    private sealed class RecordingDelay : IStableReadDelay
    {
        public ConcurrentBag<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan min, TimeSpan max, CancellationToken cancellationToken)
        {
            // Deterministic midpoint for assertions.
            TimeSpan chosen = TimeSpan.FromMilliseconds((min.TotalMilliseconds + max.TotalMilliseconds) / 2d);
            Delays.Add(chosen);
            return Task.CompletedTask;
        }
    }

    private sealed record AttemptScript(
        int FingerprintBefore,
        int FingerprintAfter,
        string Dataset,
        Action? OnFingerprint = null,
        TimeSpan? DiscoveryDelay = null,
        bool BlocksUntilCanceled = false)
    {
        public static AttemptScript Stable(string dataset, int fingerprintSeed, Action? onFingerprint = null, TimeSpan? discoveryDelay = null)
            => new(fingerprintSeed, fingerprintSeed, dataset, onFingerprint, discoveryDelay);

        public static AttemptScript HangUntilCanceled(string dataset, int fingerprintSeed)
            => new(fingerprintSeed, fingerprintSeed, dataset, BlocksUntilCanceled: true);

        public static AttemptScript Drifting(int fingerprintBefore, int fingerprintAfter)
            => new(fingerprintBefore, fingerprintAfter, "discarded");
    }

    private sealed class ScriptedAttemptFactory : IStableReadAttemptFactory<string>
    {
        private readonly Queue<AttemptScript> _scripts;

        public ScriptedAttemptFactory(IEnumerable<AttemptScript> scripts)
            => _scripts = new Queue<AttemptScript>(scripts);

        public Task<IStableReadAttemptSession<string>> OpenAsync(CancellationToken cancellationToken)
        {
            if (_scripts.Count == 0)
            {
                throw new InvalidOperationException("No attempt scripts remaining.");
            }

            return Task.FromResult<IStableReadAttemptSession<string>>(new ScriptedSession(_scripts.Dequeue()));
        }
    }

    private sealed class ScriptedSession : IStableReadAttemptSession<string>
    {
        private readonly AttemptScript _script;
        private int _fingerprintPass;

        public ScriptedSession(AttemptScript script) => _script = script;

        public Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
        {
            _script.OnFingerprint?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            int seed = _fingerprintPass++ == 0 ? _script.FingerprintBefore : _script.FingerprintAfter;
            return Task.FromResult(FingerprintFromSeed(seed));
        }

        public async Task<string> ReadCompleteDiscoveryDatasetAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
        {
            if (_script.BlocksUntilCanceled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            else if (_script.DiscoveryDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            // Ensure command timeout is plumbed (AC#8).
            Assert.Equal(context.Options.CommandTimeout, context.CommandTimeout);
            Assert.True(context.Parallelism.MaxParallelCommands <= 8);
            return _script.Dataset;
        }

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
