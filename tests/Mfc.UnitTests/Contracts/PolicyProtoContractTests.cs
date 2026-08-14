using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Xunit;

namespace Mfc.UnitTests.Contracts;

public sealed class PolicyProtoContractTests
{
    [Fact]
    public void PolicyServiceDescriptorExposesDraftAndRuleRpcs()
    {
        string[] methods = PolicyService.Descriptor.Methods.Select(m => m.Name).OrderBy(n => n).ToArray();
        Assert.Contains("CreateDraftPolicy", methods);
        Assert.Contains("GetPolicyRevision", methods);
        Assert.Contains("ListRules", methods);
        Assert.Contains("GetRule", methods);
        Assert.Contains("AddRule", methods);
        Assert.Contains("UpdateRule", methods);
        Assert.Contains("DeleteRule", methods);
        Assert.Contains("ReorderRules", methods);
        Assert.Equal("mfc.v1.PolicyService", PolicyService.Descriptor.FullName);
    }

    [Fact]
    public void C1PolicyRuleRoundTripsWithoutSecretFields()
    {
        PolicyRule original = new()
        {
            Id = new Uuid { Value = ByteString.CopyFrom(Guid.NewGuid().ToByteArray(bigEndian: true)) },
            Family = IpAddressFamily.Ipv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Ordinal = 1,
            Enabled = true,
            Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
            Description = "corp",
            Warnings =
            {
                new PolicyWarning { Code = "POLICY_SELECTOR_CATALOG_SOFT", Message = "soft" },
            },
        };
        PolicyRule clone = PolicyRule.Parser.ParseFrom(original.ToByteArray());
        Assert.Equal(original.Ordinal, clone.Ordinal);
        Assert.Equal(original.Description, clone.Description);
        Assert.Equal(original.Warnings[0].Code, clone.Warnings[0].Code);

        Assert.Equal("mfc.v1.PolicyService", PolicyService.Descriptor.FullName);
        Assert.DoesNotContain(
            PolicyService.Descriptor.Methods,
            static m => m.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                        || m.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }
}
