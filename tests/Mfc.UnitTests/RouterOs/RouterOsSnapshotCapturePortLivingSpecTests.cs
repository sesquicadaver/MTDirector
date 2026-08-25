using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Snapshot;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

/// <summary>Living Spec for P2-05 / issue #281 — production RouterOsSnapshotCapturePort.</summary>
public sealed class RouterOsSnapshotCapturePortLivingSpecTests
{
    [Fact]
    public void Ac1RouterOsSnapshotCapturePortImplementsSnapshotCapturePort()
    {
        Type type = typeof(RouterOsSnapshotCapturePort);
        Assert.True(typeof(ISnapshotCapturePort).IsAssignableFrom(type));
        Assert.Contains(type.GetConstructors(), c => c.GetParameters().Length >= 1);
    }

    [Fact]
    public void Ac2RoadmapLivingSpecRowReferencesRouterOsSnapshotCapturePort()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("RouterOsSnapshotCapturePort", roadmap, StringComparison.Ordinal);
        Assert.Contains("P2-05", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3CaptureAsyncProducesDeterministicHashesForFixtureDataset()
    {
        RouterOsDiscoveryDataset dataset = RouterOsCaptureTestFixtures.MinimalChrDataset();
        SnapshotCaptureResult first = SnapshotCaptureResultBuilder.Build(dataset);
        SnapshotCaptureResult second = SnapshotCaptureResultBuilder.Build(dataset);
        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
    }

    [Fact]
    public void Ac4RouterOsSnapshotCapturePortTypeIsInRouterOsAssembly()
    {
        Assert.Equal("Mfc.RouterOs", typeof(RouterOsSnapshotCapturePort).Assembly.GetName().Name);
    }

    [Fact]
    public void Ac5StableReadCoordinatorPortImplementsApplicationPort()
    {
        Assert.True(typeof(IStableReadCoordinatorPort).IsAssignableFrom(typeof(RouterOsStableReadCoordinatorPort)));
    }

    [Fact]
    public void Ac6SnapshotCaptureResultBuilderProducesNonEmptyRawPayload()
    {
        SnapshotCaptureResult capture = SnapshotCaptureResultBuilder.Build(RouterOsCaptureTestFixtures.MinimalChrDataset());
        Assert.True(capture.RawPayload.Length > 0);
        Assert.DoesNotContain("/login", System.Text.Encoding.UTF8.GetString(capture.RawPayload.Span), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac7FixtureStableReadCapturePortReturnsPersistReadyResult()
    {
        RouterOsDiscoveryDataset dataset = RouterOsCaptureTestFixtures.MinimalChrDataset();
        RouterOsSnapshotCapturePort port = new(new FixtureStableReadAttemptFactoryProvider(dataset));
        SnapshotCaptureResult capture = await port.CaptureAsync(MinimalTarget());
        Assert.NotEmpty(capture.SnapshotHash.ToString());
        Assert.NotEmpty(capture.Sections);
    }

    [Fact]
    public async Task Ac8UnstableStableReadSurfacesSnapshotUnstableCode()
    {
        RouterOsSnapshotCapturePort port = new(new DriftingStableReadAttemptFactoryProvider());
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.CaptureAsync(MinimalTarget()));
        Assert.StartsWith("SNAPSHOT_UNSTABLE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac9DiscoveryReaderCatalogIncludesSystemIdentityCommand()
    {
        Assert.Contains(RosReadCommandId.SystemIdentity, RouterOsDiscoveryCommandCatalog.All);
    }

    [Fact]
    public void Ac10MaterializingFactoryProviderCreatesAttemptFactory()
    {
        var provider = new MaterializingRouterOsStableReadAttemptFactoryProvider(new ThrowingMaterializer());
        IStableReadAttemptFactory<RouterOsDiscoveryDataset> factory = provider.Create(MinimalTarget());
        Assert.NotNull(factory);
    }

    private static RouterOsReadTarget MinimalTarget()
        => new()
        {
            DeviceId = new DeviceId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Endpoint = ManagementEndpoint.Create("192.0.2.1", 8729),
            SecretReference = new SecretReference(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab",
        };

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FixtureStableReadAttemptFactoryProvider(RouterOsDiscoveryDataset dataset)
        : IRouterOsStableReadAttemptFactoryProvider
    {
        public IStableReadAttemptFactory<RouterOsDiscoveryDataset> Create(RouterOsReadTarget target)
            => new FixtureStableReadAttemptFactory(dataset);
    }

    private sealed class DriftingStableReadAttemptFactoryProvider : IRouterOsStableReadAttemptFactoryProvider
    {
        public IStableReadAttemptFactory<RouterOsDiscoveryDataset> Create(RouterOsReadTarget target)
            => new DriftingStableReadAttemptFactory();
    }

    private sealed class FixtureStableReadAttemptFactory(RouterOsDiscoveryDataset dataset)
        : IStableReadAttemptFactory<RouterOsDiscoveryDataset>
    {
        public Task<IStableReadAttemptSession<RouterOsDiscoveryDataset>> OpenAsync(CancellationToken cancellationToken)
            => Task.FromResult<IStableReadAttemptSession<RouterOsDiscoveryDataset>>(new FixtureSession(dataset));
    }

    private sealed class DriftingStableReadAttemptFactory : IStableReadAttemptFactory<RouterOsDiscoveryDataset>
    {
        public Task<IStableReadAttemptSession<RouterOsDiscoveryDataset>> OpenAsync(CancellationToken cancellationToken)
            => Task.FromResult<IStableReadAttemptSession<RouterOsDiscoveryDataset>>(new DriftingSession());
    }

    private sealed class FixtureSession(RouterOsDiscoveryDataset dataset) : IStableReadAttemptSession<RouterOsDiscoveryDataset>
    {
        private static readonly ConfigurationFingerprintSet Fingerprints = BuildFingerprints();

        public Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(Fingerprints);

        public Task<RouterOsDiscoveryDataset> ReadCompleteDiscoveryDatasetAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(dataset);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static ConfigurationFingerprintSet BuildFingerprints()
        {
            List<MenuFingerprint> menus = [];
            foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
            {
                menus.Add(new MenuFingerprint
                {
                    Menu = menu,
                    Digest = Hash256.Create(new byte[Hash256.Size]),
                    Available = menu != CriticalConfigurationMenu.ManagedAnchors,
                });
            }

            return new ConfigurationFingerprintSet(menus);
        }
    }

    private sealed class DriftingSession : IStableReadAttemptSession<RouterOsDiscoveryDataset>
    {
        private int _fingerprintReads;

        public Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
        {
            int seed = Interlocked.Increment(ref _fingerprintReads);
            byte[] digest = new byte[Hash256.Size];
            digest[0] = (byte)seed;
            List<MenuFingerprint> menus = [];
            foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
            {
                menus.Add(new MenuFingerprint
                {
                    Menu = menu,
                    Digest = Hash256.Create(digest),
                    Available = true,
                });
            }

            return Task.FromResult(new ConfigurationFingerprintSet(menus));
        }

        public Task<RouterOsDiscoveryDataset> ReadCompleteDiscoveryDatasetAsync(
            StableReadExecutionContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(RouterOsCaptureTestFixtures.MinimalChrDataset());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingMaterializer : IRouterOsConnectionMaterializer
    {
        public Task<RouterOsConnectionMaterial> MaterializeAsync(
            RouterOsReadTarget target,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not used in AC10");
    }
}
