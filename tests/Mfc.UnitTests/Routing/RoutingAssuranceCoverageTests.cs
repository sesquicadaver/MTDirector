using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Routing;

/// <summary>Extra Domain/Application branch coverage for M7.1-02.</summary>
public sealed class RoutingAssuranceCoverageTests
{
    [Fact]
    public void ReconstituteRejectsZeroRowVersion()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() => RoutingAssuranceState.Reconstitute(
            DeviceId.New(),
            RoutingConfigurationSnapshot.Empty,
            RoutingOperationalSnapshot.Empty,
            RoutingAssuranceHashContract.HashConfiguration(new Dictionary<string, string>()),
            RoutingAssuranceHashContract.HashOperational(new Dictionary<string, string>()),
            [],
            [],
            [],
            DateTimeOffset.UtcNow,
            rowVersion: 0));
        Assert.Contains("row_version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithBumpsRowVersionAndRehashes()
    {
        RoutingAssuranceState original = RoutingAssuranceState.Create(
            DeviceId.New(),
            RoutingConfigurationSnapshot.Empty,
            RoutingOperationalSnapshot.Empty,
            DateTimeOffset.UtcNow);
        Dictionary<string, string> material = new(StringComparer.Ordinal) { ["rtab.main.fib"] = "yes" };
        RoutingConfigurationSnapshot nextConfig = new(
            [new RoutingTableFact { Name = "main", Fib = "yes", Disabled = null }],
            RoutingSettingsFact.Empty,
            [],
            [],
            [],
            [],
            [],
            material);
        RoutingAssuranceState updated = original.With(nextConfig, RoutingOperationalSnapshot.Empty, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(2UL, updated.RowVersion);
        Assert.NotEqual(original.ConfigurationHash, updated.ConfigurationHash);
        Assert.True(original.Equals(original));
        Assert.False(original.Equals(updated));
        Assert.False(original.Equals(null));
        _ = original.GetHashCode();
    }

    [Fact]
    public void EmptySnapshotsHashToDistinctNamespaces()
    {
        Hash256 config = RoutingAssuranceHashContract.HashConfiguration(new Dictionary<string, string>());
        Hash256 ops = RoutingAssuranceHashContract.HashOperational(new Dictionary<string, string>());
        Assert.NotEqual(config, ops);
    }

    [Fact]
    public async Task GetReturnsNotFoundAndForbidden()
    {
        FakeAuthorizationBoundary denied = new();
        denied.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        ApplicationResult<RoutingAssuranceStateView> forbidden =
            await new GetRoutingAssuranceStateUseCase(denied, new FakeRoutingAssuranceStateStore())
                .ExecuteAsync(new GetRoutingAssuranceStateQuery { Actor = "a", DeviceId = Guid.NewGuid() });
        Assert.Equal("forbidden", forbidden.Error!.Code);

        ApplicationResult<RoutingAssuranceStateView> missing =
            await new GetRoutingAssuranceStateUseCase(new FakeAuthorizationBoundary(), new FakeRoutingAssuranceStateStore())
                .ExecuteAsync(new GetRoutingAssuranceStateQuery { Actor = "a", DeviceId = Guid.NewGuid() });
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task UpsertReturnsNotFoundWhenDeviceMissingThenUpdatesExisting()
    {
        FakeDeviceStore devices = new();
        FakeRoutingAssuranceStateStore store = new();
        FakeClock clock = new();
        UpsertRoutingAssuranceStateUseCase useCase = new(new FakeAuthorizationBoundary(), devices, store, clock);

        ApplicationResult<RoutingAssuranceStateView> missing = await useCase.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = Guid.NewGuid(),
                Configuration = RoutingConfigurationSnapshot.Empty,
                OperationalState = RoutingOperationalSnapshot.Empty,
            });
        Assert.Equal("not_found", missing.Error!.Code);

        Device device = Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.10", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);
        await devices.AddAsync(device);

        ApplicationResult<RoutingAssuranceStateView> created = await useCase.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = RoutingConfigurationSnapshot.Empty,
                OperationalState = RoutingOperationalSnapshot.Empty,
            });
        Assert.True(created.IsSuccess);
        Assert.Equal(1UL, created.Value!.RowVersion);

        Dictionary<string, string> material = new(StringComparer.Ordinal) { ["rtab.main.fib"] = "yes" };
        ApplicationResult<RoutingAssuranceStateView> updated = await useCase.ExecuteAsync(
            new UpsertRoutingAssuranceStateCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                Configuration = new RoutingConfigurationSnapshot(
                    [],
                    RoutingSettingsFact.Empty,
                    [],
                    [],
                    [],
                    [],
                    [],
                    material),
                OperationalState = RoutingOperationalSnapshot.Empty,
            });
        Assert.True(updated.IsSuccess);
        Assert.Equal(2UL, updated.Value!.RowVersion);
        Assert.NotEqual(created.Value.ConfigurationHashHex, updated.Value.ConfigurationHashHex);
    }

    [Theory]
    [InlineData("route.x.active")]
    [InlineData("default.y.gateway-status")]
    public void MaterialKeyClassifierUsesLeafSegment(string key)
        => Assert.Equal(
            RoutingAssurancePropertyKind.Observation,
            RoutingAssurancePropertyClassifier.ClassifyMaterialKey(key));
}
