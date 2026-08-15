using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class ActualFilterContextMapperTests
{
    [Fact]
    public void CanonicalFilterRecordsMapToDomainRulesAndDetectPreAnchorAccept()
    {
        CanonicalRecord accept = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "0",
            ["chain"] = "forward",
            ["action"] = "accept",
            ["comment"] = "unmanaged",
            ["disabled"] = "false",
            ["src-address"] = "10.0.0.0/8",
        });
        CanonicalRecord anchor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "1",
            ["chain"] = "forward",
            ["action"] = "jump",
            ["jump-target"] = "fwc.forward.rev1",
            ["comment"] = "fwc:anchor:ipv4:forward",
            ["disabled"] = "false",
        });
        CanonicalRecord timed = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinal"] = "2",
            ["chain"] = "forward",
            ["action"] = "drop",
            ["time"] = "sunrise-sunset",
            ["disabled"] = "true",
        });

        ChainContractSet contracts = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    rejectMode: null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);
        ActualFilterAnalysisResult result = ActualFilterContextMapper.Analyze([accept, anchor, timed], [], contracts);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses);
        IReadOnlyList<ActualFilterRule> mapped = ActualFilterContextMapper.FromCanonicalFilter(
            IpAddressFamily.IPv4,
            [accept, timed]);
        Assert.Equal("10.0.0.0/8", mapped[0].KnownMatchers["src-address"]);
        Assert.Equal("sunrise-sunset", mapped[1].UnknownMatchers["time"]);
        Assert.True(mapped[1].Disabled);
    }
}
