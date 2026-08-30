using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

public sealed class ZonesDesktopServiceTests
{
    [Fact]
    public async Task CreateListBindingAndResolveSurfacesBlockers()
    {
        FakeZoneServiceClient client = new();
        ZonePanelService service = new(client);

        ZoneDefinitionListItem zone = await service.CreateCompanyZoneAsync("lan", "LAN", "corp");
        Assert.Equal("lan", zone.Key);
        Assert.Single(await service.ListZonesAsync());

        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        NodeZoneBindingListItem binding = await service.UpsertBindingAsync(
            nodeId,
            zone.Id,
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            expectedRowVersion: null);
        Assert.Equal("SingleInterface", binding.KindText);
        Assert.Contains("ether1", binding.ValuesText);

        client.ResolveBatch = new ZoneResolveBatch
        {
            Results =
            {
                new ZoneBindingResolveResult
                {
                    BindingId = ToUuid(binding.Id),
                    ZoneId = ToUuid(zone.Id),
                    DeviceId = ToUuid(Guid.Parse("99999999-8888-7777-6666-555555555555")),
                    FreshDependencyHash = new Sha256 { Value = ByteString.CopyFrom(new byte[32]) },
                    AnalysisStale = true,
                    Binding = new NodeZoneBinding
                    {
                        Id = ToUuid(binding.Id),
                        NodeId = ToUuid(nodeId),
                        ZoneId = ToUuid(zone.Id),
                        Kind = NodeZoneBindingKind.SingleInterface,
                        AnalysisStale = true,
                        RowVersion = 2,
                        ExpectedDependencyHash = new Sha256
                        {
                            Value = ByteString.CopyFrom(new byte[32]),
                        },
                    },
                    Blockers =
                    {
                        new ZoneResolveBlocker
                        {
                            Code = "ZONE_MISSING_INTERFACE",
                            Message = "missing",
                            Subject = "ether1",
                        },
                    },
                },
            },
        };
        client.ResolveBatch.Results[0].ResolvedMembers.Add("ether1");
        client.ResolveBatch.Results[0].Binding.Values.Add("ether1");

        IReadOnlyList<ZoneResolveResultListItem> results = await service.ResolveForNodeAsync(nodeId);
        Assert.Single(results);
        Assert.Contains("ZONE_MISSING_INTERFACE", results[0].BlockerLines[0]);
        Assert.True(results[0].AnalysisStale);
    }

    [Fact]
    public async Task UpdateZoneChangesNameAndClearsDescription()
    {
        FakeZoneServiceClient client = new();
        ZonePanelService service = new(client);
        ZoneDefinitionListItem created = await service.CreateCompanyZoneAsync("lan", "LAN", "corp");

        ZoneDefinitionListItem updated = await service.UpdateZoneAsync(
            created,
            "LAN-core",
            description: null,
            resetDescription: true);

        Assert.Equal("LAN-core", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(created.RowVersion + 1, updated.RowVersion);
        Assert.Equal("LAN-core", Assert.Single(await service.ListZonesAsync()).Name);
    }

    [Fact]
    public async Task ResolveForDeviceSurfacesBlockersFromDeviceRpc()
    {
        FakeZoneServiceClient client = new();
        ZonePanelService service = new(client);
        ZoneDefinitionListItem zone = await service.CreateCompanyZoneAsync("wan", "WAN", null);
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        client.ResolveBatch = new ZoneResolveBatch
        {
            Results =
            {
                new ZoneBindingResolveResult
                {
                    BindingId = ToUuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
                    ZoneId = ToUuid(zone.Id),
                    DeviceId = ToUuid(deviceId),
                    FreshDependencyHash = new Sha256 { Value = ByteString.CopyFrom(new byte[32]) },
                    AnalysisStale = false,
                    Blockers =
                    {
                        new ZoneResolveBlocker { Code = "ZONE_OBSERVATION_UNAVAILABLE", Message = "no capture" },
                    },
                },
            },
        };

        IReadOnlyList<ZoneResolveResultListItem> results = await service.ResolveForDeviceAsync(deviceId);
        Assert.Equal(deviceId, client.LastResolveDeviceId);
        ZoneResolveResultListItem row = Assert.Single(results);
        Assert.Equal(deviceId, row.DeviceId);
        Assert.Contains("ZONE_OBSERVATION_UNAVAILABLE", row.BlockerLines[0], StringComparison.Ordinal);
    }

    private static Uuid ToUuid(Guid value)
        => new() { Value = ByteString.CopyFrom(value.ToByteArray(bigEndian: true)) };

    private static Guid FromUuid(Uuid value)
        => new(value.Value.Span, bigEndian: true);

    private sealed class FakeZoneServiceClient : IZoneServiceClient
    {
        private readonly Dictionary<Guid, ZoneDefinition> _zones = [];
        private readonly Dictionary<Guid, NodeZoneBinding> _bindings = [];

        public ZoneResolveBatch ResolveBatch { get; set; } = new();

        public Guid? LastResolveDeviceId { get; private set; }

        public Task<IReadOnlyList<ZoneDefinition>> ListZoneDefinitionsAsync(
            PolicyOwnerScope? ownerScope = null,
            Guid? ownerId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ZoneDefinition>>(_zones.Values.ToArray());

        public Task<ZoneDefinition> CreateZoneDefinitionAsync(
            PolicyOwnerScope ownerScope,
            Guid? ownerId,
            string key,
            string name,
            string? description,
            CancellationToken cancellationToken = default)
        {
            ZoneDefinition zone = new()
            {
                Id = ToUuid(Guid.NewGuid()),
                OwnerScope = ownerScope,
                Key = key,
                Name = name,
                RowVersion = 1,
            };
            if (!string.IsNullOrWhiteSpace(description))
            {
                zone.Description = description;
            }

            _zones[FromUuid(zone.Id)] = zone;
            return Task.FromResult(zone);
        }

        public Task<ZoneDefinition> UpdateZoneDefinitionAsync(
            Guid zoneId,
            ulong expectedRowVersion,
            string? name,
            string? description,
            bool resetDescription,
            CancellationToken cancellationToken = default)
        {
            if (!_zones.TryGetValue(zoneId, out ZoneDefinition? zone))
            {
                throw new InvalidOperationException($"Zone '{zoneId:D}' not found.");
            }

            if (zone.RowVersion != expectedRowVersion)
            {
                throw new InvalidOperationException("Zone row_version mismatch.");
            }

            if (name is not null)
            {
                zone.Name = name;
            }

            if (resetDescription)
            {
                zone.ClearDescription();
            }
            else if (description is not null)
            {
                zone.Description = description;
            }

            zone.RowVersion++;
            return Task.FromResult(zone);
        }

        public Task DeleteZoneDefinitionAsync(
            Guid zoneId,
            ulong expectedRowVersion,
            CancellationToken cancellationToken = default)
        {
            _zones.Remove(zoneId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<NodeZoneBinding>> ListNodeZoneBindingsAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NodeZoneBinding>>(
                _bindings.Values
                    .Where(b => FromUuid(b.NodeId) == nodeId)
                    .ToArray());

        public Task<NodeZoneBinding> UpsertNodeZoneBindingAsync(
            Guid nodeId,
            Guid zoneId,
            NodeZoneBindingKind kind,
            IReadOnlyList<string> values,
            ulong? expectedRowVersion,
            CancellationToken cancellationToken = default)
        {
            NodeZoneBinding? existing = _bindings.Values.FirstOrDefault(b =>
                FromUuid(b.NodeId) == nodeId
                && FromUuid(b.ZoneId) == zoneId);
            if (existing is not null)
            {
                existing.Kind = kind;
                existing.Values.Clear();
                existing.Values.AddRange(values);
                existing.RowVersion = expectedRowVersion.GetValueOrDefault() + 1;
                existing.AnalysisStale = true;
                return Task.FromResult(existing);
            }

            NodeZoneBinding created = new()
            {
                Id = ToUuid(Guid.NewGuid()),
                NodeId = ToUuid(nodeId),
                ZoneId = ToUuid(zoneId),
                Kind = kind,
                AnalysisStale = true,
                RowVersion = 1,
                ExpectedDependencyHash = new Sha256
                {
                    Value = ByteString.CopyFrom(new byte[32]),
                },
            };
            created.Values.AddRange(values);
            _bindings[FromUuid(created.Id)] = created;
            return Task.FromResult(created);
        }

        public Task DeleteNodeZoneBindingAsync(
            Guid bindingId,
            ulong expectedRowVersion,
            CancellationToken cancellationToken = default)
        {
            _bindings.Remove(bindingId);
            return Task.CompletedTask;
        }

        public Task<ZoneResolveBatch> ResolveZonesForNodeAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ResolveBatch);

        public Task<ZoneResolveBatch> ResolveZonesForDeviceAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            LastResolveDeviceId = deviceId;
            return Task.FromResult(ResolveBatch);
        }
    }
}
