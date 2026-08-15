using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ManagementPathAnalysisTests
{
    [Fact]
    public void Ac1ApiSslDisabledIsServiceDisabled()
    {
        ManagementPathAnalysisResult result = Analyze(EnabledService() with { Disabled = true }, SafeRules());
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.ServiceDisabled);
        Assert.True(result.BlocksManagementPath);
    }

    [Fact]
    public void Ac1ApiSslMissingIsServiceDisabled()
    {
        ManagementPathAnalysisResult result = Analyze(
            ManagementIpServiceFacts.Create(found: false, disabled: true, port: null, addressPrefixes: null),
            SafeRules());
        Assert.Contains(result.Findings, f =>
            f.Code == ManagementPathAnalysisCodes.ServiceDisabled && f.Witness is not null);
    }

    [Fact]
    public void Ac1PortMismatchIsServiceDisabled()
    {
        ManagementPathAnalysisResult result = Analyze(EnabledService() with { Port = "8728" }, SafeRules());
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.ServiceDisabled);
    }

    [Fact]
    public void Ac2SourceRestrictionBlocksDisallowedPrefix()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService() with { AddressPrefixes = "10.0.0.0/8" },
            SafeRules());
        ManagementPathFinding finding = Assert.Single(result.Findings, f => f.Code == ManagementPathAnalysisCodes.SourceNotAllowed);
        Assert.NotNull(finding.Witness);
        Assert.Equal(IpProtocol.Tcp, finding.Witness.Protocol);
    }

    [Fact]
    public void UnparseableSourceRestrictionIsIndeterminate()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService() with { AddressPrefixes = "not-a-prefix" },
            SafeRules());
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);
        Assert.DoesNotContain(result.Findings, f => f.Code == ManagementPathAnalysisCodes.SourceNotAllowed);
    }

    [Fact]
    public void Ac3GuardMustExistAndPrecedeAnchor()
    {
        ManagementPathAnalysisResult missing = Analyze(EnabledService(), Anchor("input", 0), Anchor("output", 0));
        Assert.Contains(missing.Findings, f => f.Code == ManagementPathAnalysisCodes.GuardMissing);

        ManagementPathAnalysisResult moved = Analyze(
            EnabledService(),
            Anchor("input", 0),
            InputGuard(1),
            OutputGuard(0),
            Anchor("output", 1));
        Assert.Contains(moved.Findings, f => f.Code == ManagementPathAnalysisCodes.GuardMoved && f.Chain == "input");
    }

    [Fact]
    public void Ac4InvalidGuardMarkerIsIndeterminate()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService(),
            InputGuard(0, comment: "fwc:guard:"),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);
    }

    [Fact]
    public void Ac5TcpNewMustBeAllowedOnInput()
    {
        Dictionary<string, string> matchers = InputMatchers();
        matchers["connection-state"] = "established";
        ManagementPathAnalysisResult result = Analyze(
            EnabledService(),
            InputGuard(0, matchers: matchers),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.InputBlocked && f.Witness is not null);
    }

    [Fact]
    public void Ac6OutputEstablishedReplyMustBeAllowed()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService(),
            InputGuard(0),
            Anchor("input", 1),
            Anchor("output", 0));
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.OutputBlocked && f.Witness is not null);
    }

    [Fact]
    public void Ac7EachVrrpMemberIsCheckedByPhysicalAddress()
    {
        ActualFilterRule[] memberARules =
        [
            InputGuard(0, dest: "192.0.2.10"),
            OutputGuard(0, source: "192.0.2.10"),
            Anchor("input", 1),
            Anchor("output", 1),
        ];
        ManagementAccessProfile memberA = Profile(dest: "192.0.2.10", physical: ["192.0.2.10"], virtualIps: ["192.0.2.1"]);
        ManagementAccessProfile memberB = memberA.WithDestination("192.0.2.11");
        ManagementPathAnalysisResult a = Analyze(memberA, EnabledService(), memberARules);
        ManagementPathAnalysisResult b = Analyze(memberB, EnabledService(), memberARules);
        Assert.False(a.BlocksManagementPath);
        Assert.Contains(b.Findings, f => f.Code == ManagementPathAnalysisCodes.InputBlocked);
    }

    [Fact]
    public void Ac8VirtualIpIsNotTheOnlyManagementEndpoint()
    {
        ManagementAccessProfile vipOnly = Profile(dest: "192.0.2.1", physical: [], virtualIps: ["192.0.2.1"]);
        ManagementPathAnalysisResult result = Analyze(vipOnly, EnabledService(), SafeRules());
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);

        ManagementAccessProfile vipDest = Profile(dest: "192.0.2.1", physical: ["192.0.2.10"], virtualIps: ["192.0.2.1"]);
        ManagementPathAnalysisResult vipAsDest = Analyze(vipDest, EnabledService(), SafeRules());
        Assert.Contains(vipAsDest.Findings, f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);
    }

    [Fact]
    public void Ac9UnknownMatcherOnManagementPathIsBlocker()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService(),
            InputGuard(0, unknown: new Dictionary<string, string>(StringComparer.Ordinal) { ["layer7-protocol"] = "http" }),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);
        Assert.True(ManagementPathAnalysisCodes.IsFailedPrecondition(ManagementPathAnalysisCodes.PathIndeterminate));
    }

    [Fact]
    public void Ac10CandidateMustNotChangeGuard()
    {
        ManagementPathAnalysisResult result = ManagementPathAnalysis.Analyze(
            Profile(),
            EnabledService(),
            SafeRules(),
            candidateComments: ["fwc:guard:api-ssl"]);
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.GuardMoved);
    }

    [Fact]
    public void Ac11ManagementSystemTestsAreGenerated()
    {
        ManagementPathAnalysisResult result = Analyze(EnabledService(), SafeRules());
        Assert.Equal(2, result.SystemTests.Count);
        Assert.All(result.SystemTests, t =>
        {
            Assert.Equal(ManagementSystemTest.OriginSystem, t.Origin);
            Assert.Equal(ManagementSystemTest.ExpectedAccept, t.Expected);
        });
        Assert.Equal(PolicyFilterChain.Input, result.SystemTests[0].Chain);
        Assert.Equal(ConnectionState.New, result.SystemTests[0].Packet.ConnectionState);
        Assert.Equal(PolicyFilterChain.Output, result.SystemTests[1].Chain);
        Assert.Equal(ConnectionState.Established, result.SystemTests[1].Packet.ConnectionState);
        Assert.Equal((ushort)8729, result.SystemTests[0].Packet.DestinationPort);
    }

    [Fact]
    public void Ac12SafetyFindingHasWitnessWhenPossible()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService(),
            Rule("input", 0, "drop"),
            InputGuard(1),
            OutputGuard(0),
            Anchor("input", 2),
            Anchor("output", 1));
        ManagementPathFinding finding = Assert.Single(result.Findings, f => f.Code == ManagementPathAnalysisCodes.InputBlocked);
        Assert.NotNull(finding.Witness);
        Assert.Equal("192.0.2.0", finding.Witness.SourceAddress);
        Assert.Equal("192.0.2.10", finding.Witness.DestinationAddress);
    }

    [Fact]
    public void UnmanagedPreGuardFastTrackIsIndeterminate()
    {
        ManagementPathAnalysisResult result = Analyze(
            EnabledService(),
            Rule("input", 0, "fasttrack-connection"),
            InputGuard(1),
            OutputGuard(0),
            Anchor("input", 2),
            Anchor("output", 1));
        Assert.Contains(result.Findings, f =>
            f.Code == ManagementPathAnalysisCodes.PathIndeterminate && f.Chain == "input" && f.Ordinal == 0);
    }

    [Fact]
    public void ProvenPathHasNoBlockersAndDoesNotUseImplicitAccept()
    {
        ManagementPathAnalysisResult result = Analyze(EnabledService(), SafeRules());
        Assert.False(result.BlocksManagementPath);
        Assert.Empty(result.Findings);
        Assert.Equal(32, result.ManagementPathContextHash.Bytes.Length);
    }

    [Fact]
    public void OutOfBandFlagDoesNotSkipInBandApiSslChecks()
    {
        ManagementAccessProfile profile = Profile(outOfBand: true);
        ManagementPathAnalysisResult result = ManagementPathAnalysis.Analyze(
            profile,
            EnabledService() with { Disabled = true },
            SafeRules());
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.ServiceDisabled);
    }

    [Fact]
    public void DnsDestinationIsIndeterminate()
    {
        ManagementPathAnalysisResult result = Analyze(
            Profile(dest: "router.example.test"),
            EnabledService(),
            SafeRules());
        Assert.Contains(result.Findings, f => f.Code == ManagementPathAnalysisCodes.PathIndeterminate);
        Assert.Empty(result.SystemTests);
    }

    [Fact]
    public void ManagementPathHashEntersAnalysisContextWithoutChangingPriorPreimages()
    {
        ManagementPathAnalysisResult first = Analyze(EnabledService(), SafeRules());
        ManagementPathAnalysisResult second = Analyze(EnabledService(), SafeRules());
        Assert.Equal(first.ManagementPathContextHash.ToString(), second.ManagementPathContextHash.ToString());

        Hash256 actual = ActualFilterAnalysis.HashActualContext([]);
        Hash256 packet = PacketPathAnalysis.HashPacketPathContext([]);
        Hash256 combined = ManagementPathAnalysis.HashAnalysisContext(actual, packet, first.ManagementPathContextHash);
        Assert.Equal(
            combined.ToString(),
            ManagementPathAnalysis.HashAnalysisContext(actual, packet, first.ManagementPathContextHash).ToString());
        Assert.NotEqual(ActualFilterAnalysis.HashAnalysisContext(actual).ToString(), combined.ToString());
        Assert.NotEqual(PacketPathAnalysis.HashAnalysisContext(actual, packet).ToString(), combined.ToString());

        ManagementPathAnalysisResult changed = Analyze(EnabledService() with { Port = "8728" }, SafeRules());
        Assert.NotEqual(first.ManagementPathContextHash.ToString(), changed.ManagementPathContextHash.ToString());
    }

    [Fact]
    public void ProfileAndCodeInvariantsHold()
    {
        Assert.False(ManagementPathAnalysisCodes.IsFailedPrecondition(string.Empty));
        Assert.True(ManagementPathAnalysisCodes.IsFailedPrecondition(ManagementPathAnalysisCodes.GuardMissing));
        Assert.Throws<DomainInvariantException>(() =>
            ManagementAccessProfile.Create([], "192.0.2.10", 8729));
        Assert.Throws<DomainInvariantException>(() =>
            ManagementAccessProfile.Create([AddressPrefix.Parse("192.0.2.0/24")], "  ", 8729));
        Assert.Throws<DomainInvariantException>(() =>
            ManagementAccessProfile.Create([AddressPrefix.Parse("192.0.2.0/24")], "192.0.2.10", 0));
        Assert.True(ActualFilterMarker.IsGuard("fwc:guard:api-ssl"));
        Assert.True(ActualFilterMarker.IsValidGuardMarker("mfc:guard:v1:0123456789abcdef:4:i:0"));
        Assert.False(ActualFilterMarker.IsValidGuardMarker("fwc:guard:"));
        Assert.False(ActualFilterMarker.IsGuard("fwc:anchor:ipv4:input"));
    }

    private static ManagementPathAnalysisResult Analyze(
        ManagementIpServiceFacts service,
        params ActualFilterRule[] rules)
        => Analyze(Profile(), service, rules);

    private static ManagementPathAnalysisResult Analyze(
        ManagementAccessProfile profile,
        ManagementIpServiceFacts service,
        params ActualFilterRule[] rules)
        => ManagementPathAnalysis.Analyze(profile, service, rules);

    private static ManagementAccessProfile Profile(
        string dest = "192.0.2.10",
        bool outOfBand = false,
        IReadOnlyList<string>? physical = null,
        IReadOnlyList<string>? virtualIps = null)
        => ManagementAccessProfile.Create(
            [AddressPrefix.Parse("192.0.2.0/24")],
            dest,
            ManagementPathAnalysis.DefaultApiSslPort,
            outOfBandIndependent: outOfBand,
            physicalManagementAddresses: physical,
            virtualManagementAddresses: virtualIps);

    private static ManagementIpServiceFacts EnabledService()
        => ManagementIpServiceFacts.Create(found: true, disabled: false, port: "8729", addressPrefixes: null);

    private static ActualFilterRule[] SafeRules()
        =>
        [
            InputGuard(0),
            Anchor("input", 1),
            OutputGuard(0),
            Anchor("output", 1),
        ];

    private static ActualFilterRule InputGuard(
        int ordinal,
        string? comment = "fwc:guard:api-ssl",
        string dest = "192.0.2.10",
        IReadOnlyDictionary<string, string>? matchers = null,
        IReadOnlyDictionary<string, string>? unknown = null)
        => Rule(
            "input",
            ordinal,
            "accept",
            comment: comment,
            known: matchers ?? InputMatchers(dest),
            unknown: unknown);

    private static ActualFilterRule OutputGuard(int ordinal, string source = "192.0.2.10")
        => Rule(
            "output",
            ordinal,
            "accept",
            comment: "fwc:guard:api-ssl",
            known: OutputMatchers(source));

    private static ActualFilterRule Anchor(string chain, int ordinal)
        => Rule(
            chain,
            ordinal,
            "jump",
            jumpTarget: $"fwc.{chain}.rev1",
            comment: $"fwc:anchor:ipv4:{chain}");

    private static Dictionary<string, string> InputMatchers(string dest = "192.0.2.10")
        => new(StringComparer.Ordinal)
        {
            ["protocol"] = "tcp",
            ["src-address"] = "192.0.2.0/24",
            ["dst-address"] = dest,
            ["dst-port"] = "8729",
            ["connection-state"] = "new,established",
        };

    private static Dictionary<string, string> OutputMatchers(string source = "192.0.2.10")
        => new(StringComparer.Ordinal)
        {
            ["protocol"] = "tcp",
            ["src-address"] = source,
            ["src-port"] = "8729",
            ["dst-address"] = "192.0.2.0/24",
            ["connection-state"] = "established,related",
        };

    private static ActualFilterRule Rule(
        string chain,
        int ordinal,
        string action,
        string? comment = null,
        string? jumpTarget = null,
        IReadOnlyDictionary<string, string>? known = null,
        IReadOnlyDictionary<string, string>? unknown = null)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            chain,
            ordinal,
            action,
            jumpTarget: jumpTarget,
            comment: comment,
            knownMatchers: known,
            unknownMatchers: unknown);
}
