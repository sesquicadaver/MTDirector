using Mfc.Application.Inventory;
using Mfc.Application.Mapping;
using Mfc.Domain.Inventory;
using Xunit;

namespace Mfc.UnitTests.Application;

/// <summary>W6-05: GetNode Reachability from LastSupportState + process-local observation.</summary>
public sealed class DeviceReachabilityProjectorTests
{
    [Fact]
    public void WithoutSupportStateStaysUnknown()
        => Assert.Equal(DeviceReachabilityProjector.Unknown, DeviceReachabilityProjector.FromSupportState(null));

    [Fact]
    public void SupportStateProjectsReachable()
        => Assert.Equal(
            DeviceReachabilityProjector.Reachable,
            DeviceReachabilityProjector.FromSupportState(SupportState.Supported));

    [Fact]
    public void ObservationOverridesSupportState()
    {
        Assert.Equal(
            DeviceReachabilityProjector.Unreachable,
            DeviceReachabilityProjector.Project(SupportState.Supported, DeviceReachabilityProjector.Unreachable));
        Assert.Equal(
            DeviceReachabilityProjector.Reachable,
            DeviceReachabilityProjector.Project(null, DeviceReachabilityProjector.Reachable));
    }

    [Fact]
    public void InMemoryStoreRoundTripsUnreachable()
    {
        InMemoryDeviceReachabilityObservationStore store = new();
        Guid deviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        store.Record(deviceId, DeviceReachabilityProjector.Unreachable);
        Assert.True(store.TryGet(deviceId, out string reachability));
        Assert.Equal(DeviceReachabilityProjector.Unreachable, reachability);
    }
}
