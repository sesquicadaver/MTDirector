using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class VrrpMemberLiveFactsObserverTests
{
    [Fact]
    public void IndependentTrafficIgnoresVrrpAndDisabledInterfaces()
    {
        Assert.False(VrrpMemberLiveFactsObserver.HasProvenIndependentRoutedTraffic(
        [
            Row(("name", "vrrp1"), ("type", "vrrp"), ("running", "true"), ("rx-byte", "100"), ("tx-byte", "100")),
            Row(("name", "ether1"), ("type", "ether"), ("disabled", "yes"), ("running", "true"), ("rx-byte", "50"), ("tx-byte", "50")),
            Row(("name", "ether2"), ("type", "ether"), ("running", "false"), ("rx-byte", "50"), ("tx-byte", "50")),
        ]));
    }

    [Fact]
    public void IndependentTrafficRequiresPositiveCountersOnRunningNonVrrp()
    {
        Assert.True(VrrpMemberLiveFactsObserver.HasProvenIndependentRoutedTraffic(
        [
            Row(("name", "ether1"), ("type", "ether"), ("running", "true"), ("rx-byte", "0"), ("tx-byte", "0"), ("rx-packet", "12"), ("tx-packet", "0")),
        ]));
        Assert.False(VrrpMemberLiveFactsObserver.HasProvenIndependentRoutedTraffic(
        [
            Row(("name", "ether1"), ("type", "ether"), ("running", "true"), ("rx-byte", "0"), ("tx-byte", "0")),
        ]));
    }

    [Fact]
    public void InterfacesProfileRequestsTrafficCounters()
    {
        RosPropertyProfile interfaces = RosReadCommandRegistry.Get(RosReadCommandId.Interfaces).PropertyProfile;
        Assert.True(interfaces.TryGet("rx-byte", out RosPropertyDefinition? rx));
        Assert.Equal(RosPropertyClassification.ObservationTyped, rx!.Classification);
        Assert.True(interfaces.TryGet("tx-packet", out RosPropertyDefinition? txp));
        Assert.Equal(RosPropertyClassification.ObservationTyped, txp!.Classification);
    }

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
