using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

public sealed class InventoryTreeServiceTests
{
    [Fact]
    public async Task RefreshBuildsSiteNodeDeviceHierarchy()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        FakeInventoryTreeClient client = new()
        {
            Sites =
            [
                new Site
                {
                    Id = ToUuid(siteId),
                    Code = "LAB",
                    Name = "Lab",
                    Status = SiteStatus.Active,
                },
            ],
            NodesBySite =
            {
                [siteId] =
                [
                    new Node
                    {
                        Id = ToUuid(nodeId),
                        SiteId = ToUuid(siteId),
                        Name = "core",
                        DeclaredKind = NodeKind.Router,
                        DeclaredUplinkMode = DeclaredUplinkMode.One,
                        Status = NodeStatus.Active,
                    },
                ],
            },
            NodeDetailsById =
            {
                [nodeId] = new NodeDetails
                {
                    Node = new Node
                    {
                        Id = ToUuid(nodeId),
                        SiteId = ToUuid(siteId),
                        Name = "core",
                        DeclaredKind = NodeKind.Router,
                        DeclaredUplinkMode = DeclaredUplinkMode.One,
                        Status = NodeStatus.Active,
                    },
                    Devices =
                    {
                        new Device
                        {
                            Id = ToUuid(deviceId),
                            NodeId = ToUuid(nodeId),
                            DisplayName = "r1",
                            ManagementHost = "192.0.2.1",
                            ManagementPort = 8729,
                            Enabled = true,
                            LastSupportState = SupportState.Supported,
                            Reachability = "Unknown",
                        },
                    },
                },
            },
        };

        InventoryTreeService service = new(client);
        InventoryTreeLoadResult result = await service.RefreshAsync();

        Assert.True(result.Succeeded);
        Assert.False(result.IsCached);
        Assert.Single(result.Roots);
        InventoryTreeItem site = result.Roots[0];
        Assert.Equal(InventoryTreeKind.Site, site.Kind);
        Assert.Single(site.Children);
        InventoryTreeItem node = site.Children[0];
        Assert.Equal(InventoryTreeKind.Node, node.Kind);
        Assert.Equal("Router", node.NodeKindText);
        Assert.Equal("One", node.UplinkModeText);
        Assert.Single(node.Children);
        InventoryTreeItem device = node.Children[0];
        Assert.Equal(InventoryTreeKind.Device, device.Kind);
        Assert.Equal("r1", device.DisplayName);
        Assert.Equal("Unknown", device.ReachabilityText);
        Assert.Equal("—", device.RouterOsVersionText);
        Assert.Equal("—", device.ModelText);
        Assert.Equal("—", device.VrrpRolesText);
        Assert.False(device.IsVrrpMember);
    }

    [Fact]
    public async Task MapDeviceKeepsReachabilityModelVersionVrrpAndLastSnapshot()
    {
        Guid siteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        DateTime snapshotUtc = new(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc);
        FakeInventoryTreeClient client = new()
        {
            Sites =
            [
                new Site
                {
                    Id = ToUuid(siteId),
                    Code = "LAB",
                    Name = "Lab",
                    Status = SiteStatus.Active,
                },
            ],
            NodesBySite =
            {
                [siteId] =
                [
                    new Node
                    {
                        Id = ToUuid(nodeId),
                        SiteId = ToUuid(siteId),
                        Name = "pair",
                        DeclaredKind = NodeKind.Vrrp,
                        DeclaredUplinkMode = DeclaredUplinkMode.One,
                        Status = NodeStatus.Active,
                    },
                ],
            },
            NodeDetailsById =
            {
                [nodeId] = new NodeDetails
                {
                    Node = new Node
                    {
                        Id = ToUuid(nodeId),
                        SiteId = ToUuid(siteId),
                        Name = "pair",
                        DeclaredKind = NodeKind.Vrrp,
                        DeclaredUplinkMode = DeclaredUplinkMode.One,
                        Status = NodeStatus.Active,
                    },
                    Devices =
                    {
                        new Device
                        {
                            Id = ToUuid(deviceId),
                            NodeId = ToUuid(nodeId),
                            DisplayName = "r1",
                            ManagementHost = "192.0.2.1",
                            ManagementPort = 8729,
                            Enabled = true,
                            LastSupportState = SupportState.Supported,
                            Reachability = "Reachable",
                            RouterosVersion = "7.16.2",
                            Model = "CHR",
                            LastSnapshotAt = Timestamp.FromDateTime(snapshotUtc),
                            VrrpRoleLabels = { "master" },
                        },
                    },
                },
            },
        };

        InventoryTreeService service = new(client);
        InventoryTreeLoadResult result = await service.RefreshAsync();

        InventoryTreeItem device = Assert.Single(Assert.Single(result.Roots).Children[0].Children);
        Assert.Equal("Reachable", device.ReachabilityText);
        Assert.Equal("CHR", device.ModelText);
        Assert.Equal("7.16.2", device.RouterOsVersionText);
        Assert.Equal("master", device.VrrpRolesText);
        Assert.True(device.IsVrrpMember);
        Assert.Equal("2026-08-30 10:00:00Z", device.LastSnapshotText);
        Assert.Equal("192.0.2.1:8729", device.ManagementHostText);
    }

    [Fact]
    public async Task ParallelRefreshDoesNotStartTwoOverlappingLoads()
    {
        Guid siteId = Guid.NewGuid();
        FakeInventoryTreeClient client = new()
        {
            Sites = [new Site { Id = ToUuid(siteId), Code = "S1", Name = "One", Status = SiteStatus.Active }],
            ListSitesDelay = TimeSpan.FromMilliseconds(200),
        };
        client.NodesBySite[siteId] = [];
        InventoryTreeService service = new(client);

        Task<InventoryTreeLoadResult> first = service.RefreshAsync();
        Task<InventoryTreeLoadResult> second = service.RefreshAsync();
        InventoryTreeLoadResult[] results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Equal(1, client.ListSitesCalls);
    }

    [Fact]
    public async Task FailedRefreshPreservesPreviousTreeAndSetsCached()
    {
        Guid siteId = Guid.NewGuid();
        FakeInventoryTreeClient client = new()
        {
            Sites = [new Site { Id = ToUuid(siteId), Code = "OK", Name = "Ok", Status = SiteStatus.Active }],
        };
        client.NodesBySite[siteId] = [];
        InventoryTreeService service = new(client);

        InventoryTreeLoadResult ok = await service.RefreshAsync();
        Assert.True(ok.Succeeded);
        Assert.Single(ok.Roots);

        client.FailListSites = true;
        InventoryTreeLoadResult failed = await service.RefreshAsync();

        Assert.False(failed.Succeeded);
        Assert.True(failed.IsCached);
        Assert.Single(failed.Roots);
        Assert.Equal("OK — Ok", failed.Roots[0].DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
    }

    [Fact]
    public async Task CancellationStopsRefresh()
    {
        FakeInventoryTreeClient client = new()
        {
            Sites = [new Site { Id = ToUuid(Guid.NewGuid()), Code = "C1", Name = "Cancel", Status = SiteStatus.Active }],
            ListSitesDelay = TimeSpan.FromSeconds(5),
        };
        InventoryTreeService service = new(client);
        using CancellationTokenSource cts = new();
        Task<InventoryTreeLoadResult> refresh = service.RefreshAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await refresh);
        Assert.True(client.ListSitesCalls <= 1);
        Assert.False(service.Current.IsRefreshing);
        Assert.Empty(service.Current.Roots);
    }

    [Fact]
    public void InventoryTreeViewModelAssemblyHasNoDomainOrRouterOsReferences()
    {
        System.Reflection.Assembly desktop = typeof(Mfc.Desktop.App).Assembly;
        string[] referenced = desktop.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain("Mfc.Domain", referenced);
        Assert.DoesNotContain("Mfc.RouterOs", referenced);
        Assert.DoesNotContain("Mfc.Application", referenced);
        Assert.DoesNotContain("Mfc.Infrastructure", referenced);
        Assert.Contains("Mfc.Contracts", referenced);
    }

    private static Uuid ToUuid(Guid id)
        => new() { Value = ByteString.CopyFrom(id.ToByteArray(bigEndian: true)) };

    private sealed class FakeInventoryTreeClient : IInventoryTreeClient
    {
        public List<Site> Sites { get; init; } = [];

        public Dictionary<Guid, List<Node>> NodesBySite { get; } = [];

        public Dictionary<Guid, NodeDetails> NodeDetailsById { get; } = [];

        public TimeSpan ListSitesDelay { get; set; }

        public bool FailListSites { get; set; }

        public int ListSitesCalls { get; private set; }

        public async Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
        {
            ListSitesCalls++;
            if (ListSitesDelay > TimeSpan.Zero)
            {
                await Task.Delay(ListSitesDelay, cancellationToken).ConfigureAwait(false);
            }

            if (FailListSites)
            {
                throw new InvalidOperationException("simulated inventory failure");
            }

            return Sites;
        }

        public Task<IReadOnlyList<Node>> ListAllNodesAsync(
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<Node> nodes = NodesBySite.TryGetValue(siteId, out List<Node>? list)
                ? list
                : [];
            return Task.FromResult(nodes);
        }

        public Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!NodeDetailsById.TryGetValue(nodeId, out NodeDetails? details))
            {
                details = new NodeDetails
                {
                    Node = new Node
                    {
                        Id = ToUuid(nodeId),
                        Name = "missing",
                        DeclaredKind = NodeKind.Router,
                        DeclaredUplinkMode = DeclaredUplinkMode.None,
                        Status = NodeStatus.Draft,
                    },
                };
            }

            return Task.FromResult(details);
        }

        public Task<NodeWorkflow> GetNodeWorkflowAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("GetNodeWorkflow is not used by InventoryTreeService tests.");
        }

        public Task<Site> CreateSiteAsync(string code, string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("CreateSite is not used by InventoryTreeService tests.");
        }

        public Task<Node> CreateNodeAsync(
            Guid siteId,
            string name,
            NodeKind declaredKind,
            DeclaredUplinkMode declaredUplinkMode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("CreateNode is not used by InventoryTreeService tests.");
        }

        public Task<Device> RegisterDeviceAsync(
            Guid nodeId,
            string displayName,
            string managementHost,
            uint managementPort,
            DeviceRole role,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("RegisterDevice is not used by InventoryTreeService tests.");
        }

        public Task<DeviceConnectionSummary> UpdateDeviceConnectionAsync(
            Guid deviceId,
            string username,
            ReadOnlyMemory<byte> passwordUtf8,
            CertificateTrustMode trustMode,
            string? caProfileRef,
            Sha256? pinnedSpkiSha256,
            uint connectTimeoutMs,
            uint commandTimeoutMs,
            ulong maxResponseBytes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("UpdateDeviceConnection is not used by InventoryTreeService tests.");
        }

        public Task<ListNeighborCandidatesResponse> ListNeighborCandidatesAsync(
            Guid seedDeviceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("ListNeighborCandidates is not used by InventoryTreeService tests.");
        }

        public Task<ValidateDeviceConnectionResponse> ValidateDeviceConnectionAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("ValidateDeviceConnection is not used by InventoryTreeService tests.");
        }
    }
}
