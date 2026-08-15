using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class ManagementPathBlockerMapperTests
{
    [Fact]
    public void DiscoveryMapsApiSslAddressAndFilterWithoutCreatingGuards()
    {
        ApiSslServiceDiscovery apiSsl = new()
        {
            Found = true,
            Disabled = false,
            Port = "8729",
            AddressPrefixes = "10.0.0.0/8",
            Certificate = "api-ssl",
            TlsVersion = "only-1.2",
            Vrf = null,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
        RosReadCommandResult ipv4 = Ok(
            RosReadCommandId.Ipv4Filter,
            Row(
                ("chain", "input"),
                ("action", "accept"),
                ("comment", "fwc:guard:api-ssl"),
                ("protocol", "tcp"),
                ("src-address", "192.0.2.0/24"),
                ("dst-address", "192.0.2.10"),
                ("dst-port", "8729"),
                ("connection-state", "new,established")),
            Row(
                ("chain", "input"),
                ("action", "jump"),
                ("jump-target", "fwc.input.rev1"),
                ("comment", "fwc:anchor:ipv4:input")),
            Row(("chain", "output"), ("action", "drop"), ("dynamic", "true"), ("comment", "dyn")));
        FirewallFilterDiscoveryResult discovered = FirewallFilterDiscovery.BuildResult(
            ipv4,
            Ok(RosReadCommandId.Ipv6Filter),
            Ok(RosReadCommandId.Ipv4AddressLists),
            Ok(RosReadCommandId.Ipv6AddressLists));

        ManagementAccessProfile profile = ManagementAccessProfile.Create(
            [AddressPrefix.Parse("192.0.2.0/24")],
            "192.0.2.10",
            8729);
        ManagementPathAnalysisResult result = ManagementPathBlockerMapper.Analyze(profile, apiSsl, discovered);
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.SourceNotAllowed);
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.OutputBlocked);
        Assert.Equal("10.0.0.0/8", ManagementPathBlockerMapper.FromApiSsl(apiSsl).AddressPrefixes);
        Assert.Contains(ActualFilterRuleMapper.FromDiscovery(discovered), r => r.Dynamic);
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
