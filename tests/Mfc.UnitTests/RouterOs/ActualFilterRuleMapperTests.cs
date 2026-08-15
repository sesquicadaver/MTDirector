using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class ActualFilterRuleMapperTests
{
    [Fact]
    public void DiscoveryMapsDynamicJumpAndUnknownMatchers()
    {
        RosReadCommandResult ipv4 = Ok(
            RosReadCommandId.Ipv4Filter,
            Row(("chain", "forward"), ("action", "accept"), ("comment", "unmanaged"), ("disabled", "false")),
            Row(
                ("chain", "forward"),
                ("action", "jump"),
                ("jump-target", "fwc.forward.rev1"),
                ("comment", "fwc:anchor:ipv4:forward"),
                ("disabled", "false")),
            Row(("chain", "forward"), ("action", "drop"), ("dynamic", "true"), ("comment", "dyn")));
        RosReadCommandResult mystery = new()
        {
            CommandId = RosReadCommandId.Ipv4Filter,
            Lifecycle = RosCommandLifecycle.Completed,
            SessionInvalidated = false,
            Error = null,
            Records =
            [
                new RosReadRecord
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["chain"] = "input",
                        ["action"] = "accept",
                        ["comment"] = "x",
                    },
                    RawProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["mystery-matcher"] = "keep",
                        ["protocol"] = "tcp",
                    },
                },
            ],
        };

        FirewallFilterDiscoveryResult discovered = FirewallFilterDiscovery.BuildResult(
            ipv4,
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));
        IReadOnlyList<ActualFilterRule> mapped = ActualFilterRuleMapper.FromDiscovery(discovered);
        Assert.Equal(3, mapped.Count);
        Assert.Equal("fwc.forward.rev1", mapped[1].JumpTarget);
        Assert.True(mapped[2].Dynamic);
        Assert.Equal(2, mapped[2].Ordinal);

        FirewallFilterDiscoveryResult unknown = FirewallFilterDiscovery.BuildResult(
            mystery,
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));
        ActualFilterRule mysteryRule = Assert.Single(ActualFilterRuleMapper.FromDiscovery(unknown));
        Assert.Equal("keep", mysteryRule.UnknownMatchers["mystery-matcher"]);
        Assert.Equal("tcp", mysteryRule.KnownMatchers["protocol"]);
        Assert.False(mysteryRule.UnknownMatchers.ContainsKey("protocol"));
        Assert.Equal(IpAddressFamily.IPv4, mysteryRule.Family);
    }

    private static RosReadCommandResult Ok(RosReadCommandId id, params RosReadRecord[] rows)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

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
