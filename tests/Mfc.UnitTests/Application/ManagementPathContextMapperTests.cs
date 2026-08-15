using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class ManagementPathContextMapperTests
{
    [Fact]
    public void CanonicalIpServicesAndFilterMapToDomainBlockersWithoutRewritingGuards()
    {
        CanonicalRecord services = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["api-ssl.disabled"] = "true",
            ["api-ssl.port"] = "8729",
            ["api-ssl.address"] = "10.0.0.0/8",
        });
        CanonicalRecord inputGuard = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "input",
            ["action"] = "accept",
            ["comment"] = "fwc:guard:api-ssl",
            ["protocol"] = "tcp",
            ["src-address"] = "192.0.2.0/24",
            ["dst-address"] = "192.0.2.10",
            ["dst-port"] = "8729",
            ["connection-state"] = "new,established",
        });
        CanonicalRecord inputAnchor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "1",
            ["chain"] = "input",
            ["action"] = "jump",
            ["jump-target"] = "fwc.input.rev1",
            ["comment"] = "fwc:anchor:ipv4:input",
        });

        ManagementAccessProfile profile = ManagementAccessProfile.Create(
            [AddressPrefix.Parse("192.0.2.0/24")],
            "192.0.2.10",
            8729);
        ManagementPathAnalysisResult result = ManagementPathContextMapper.Analyze(
            profile,
            [services],
            [inputGuard, inputAnchor],
            []);
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.ServiceDisabled);
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.OutputBlocked);

        ManagementIpServiceFacts mapped = ManagementPathContextMapper.FromCanonicalIpServices([services]);
        Assert.True(mapped.Found);
        Assert.True(mapped.Disabled);
        Assert.Equal("10.0.0.0/8", mapped.AddressPrefixes);
    }
}
