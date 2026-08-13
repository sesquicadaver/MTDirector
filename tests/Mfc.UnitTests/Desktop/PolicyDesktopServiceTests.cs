using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

public sealed class PolicyDesktopServiceTests
{
    [Fact]
    public async Task U1ListRulesSurfacesOrdinalEffectAndWarnings()
    {
        FakePolicyServiceClient client = new();
        Guid revisionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid ruleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        client.Response = new ListRulesResponse
        {
            RevisionId = ToUuid(revisionId),
            ContentHash = new Sha256 { Value = ByteString.CopyFrom(new byte[32]) },
            Rules =
            {
                new PolicyRule
                {
                    Id = ToUuid(ruleId),
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Ordinal = 0,
                    Enabled = true,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "allow-lan",
                    Warnings =
                    {
                        new PolicyWarning
                        {
                            Code = "POLICY_SELECTOR_CATALOG_SOFT",
                            Message = "soft",
                            Subject = "address",
                        },
                    },
                },
            },
        };

        PolicyPanelService service = new(client);
        IReadOnlyList<PolicyRuleListItem> items = await service.ListRulesAsync(revisionId);
        Assert.Single(items);
        Assert.Equal(0u, items[0].Ordinal);
        Assert.Contains("Accept", items[0].EffectText, StringComparison.Ordinal);
        Assert.Contains("POLICY_SELECTOR_CATALOG_SOFT", items[0].WarningLines[0], StringComparison.Ordinal);
        Assert.Contains("allow-lan", items[0].SummaryLine, StringComparison.Ordinal);
    }

    private static Uuid ToUuid(Guid value)
        => new() { Value = ByteString.CopyFrom(value.ToByteArray(bigEndian: true)) };

    private sealed class FakePolicyServiceClient : IPolicyServiceClient
    {
        public ListRulesResponse Response { get; set; } = new();

        public Task<ListRulesResponse> ListRulesAsync(
            Guid revisionId,
            bool activeOnly = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Response);

        public Task<PolicyRevision> GetPolicyRevisionAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PolicyRevision { Id = ToUuid(revisionId) });
    }
}
