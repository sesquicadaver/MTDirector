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
        Assert.Contains("UpdateExceptionMetadata", methods);
        Assert.Contains("RecordAnalysisRun", methods);
        Assert.Contains("AcknowledgeWarning", methods);
        Assert.Contains("SubmitRevisionForReview", methods);
        Assert.Contains("ApproveRevision", methods);
        Assert.Contains("ActivateDesiredBinding", methods);
        Assert.Contains("ExpireExceptionBinding", methods);
        Assert.Contains("ComposeEffectivePolicy", methods);
        Assert.Contains("CreateDraftPolicy", methods);
        Assert.Contains("ListPolicies", methods);
        Assert.Contains("GetPolicyRevision", methods);
        Assert.Contains("ListRules", methods);
        Assert.Contains("GetRule", methods);
        Assert.Contains("AddRule", methods);
        Assert.Contains("UpdateRule", methods);
        Assert.Contains("DeleteRule", methods);
        Assert.Contains("ReorderRules", methods);
        Assert.Contains("ValidateRevision", methods);
        Assert.Contains("UpsertAddressObject", methods);
        Assert.Contains("UpsertServiceObject", methods);
        Assert.Contains("ReplaceChainContracts", methods);
        Assert.Contains("ReplacePolicyTests", methods);
        Assert.Contains("DiffPolicyRevisions", methods);
        Assert.Contains("GetDevicePolicySafetyAnalysis", methods);
        Assert.Equal("mfc.v1.PolicyService", PolicyService.Descriptor.FullName);
    }

    [Fact]
    public void ListPoliciesCatalogMessagesExposeLatestRevisionIdentity()
    {
        Assert.Contains(PolicyService.Descriptor.Methods, static m => m.Name == "ListPolicies");
        string[] request = ListPoliciesRequest.Descriptor.Fields.InDeclarationOrder()
            .Select(static f => f.Name)
            .ToArray();
        Assert.Equal(["kind"], request);
        string[] item = PolicyCatalogItem.Descriptor.Fields.InDeclarationOrder()
            .Select(static f => f.Name)
            .ToArray();
        Assert.Equal(
            [
                "policy_id",
                "name",
                "kind",
                "owner_scope",
                "owner_id",
                "latest_revision_id",
                "latest_revision_number",
                "latest_revision_state",
                "content_hash",
            ],
            item);
        Assert.Equal("policies", ListPoliciesResponse.Descriptor.FindFieldByNumber(1)!.Name);
        Assert.True(ListPoliciesResponse.Descriptor.FindFieldByNumber(1)!.IsRepeated);
    }

    [Fact]
    public void GetDevicePolicySafetyAnalysisExposesHashesFindingsAndWitnesses()
    {
        Assert.Contains(PolicyService.Descriptor.Methods, static m => m.Name == "GetDevicePolicySafetyAnalysis");
        string[] request = GetDevicePolicySafetyAnalysisRequest.Descriptor.Fields.InDeclarationOrder()
            .Select(static f => f.Name)
            .ToArray();
        Assert.Equal(["device_id", "revision_id", "controller_source_prefixes"], request);
        string[] analysis = PolicySafetyAnalysis.Descriptor.Fields.InDeclarationOrder()
            .Select(static f => f.Name)
            .ToArray();
        Assert.Equal(
            [
                "device_id",
                "capture_id",
                "revision_id",
                "management_path_context_hash",
                "fasttrack_context_hash",
                "blocks_management_path",
                "allows_safe_fasttrack",
                "requires_accept_fallback",
                "risk_floor",
                "management_path_findings",
                "fasttrack_findings",
                "system_tests",
            ],
            analysis);
        Assert.True(PolicySafetyFinding.Descriptor.FindFieldByNumber(5)!.MessageType == PolicyWitnessPacket.Descriptor);
    }

    [Theory]
    [InlineData("ValidateRevision")]
    [InlineData("UpsertAddressObject")]
    [InlineData("UpsertServiceObject")]
    [InlineData("ReplaceChainContracts")]
    [InlineData("ReplacePolicyTests")]
    [InlineData("DiffPolicyRevisions")]
    public void M218AuthoringReviewRpcsArePresent(string rpcName)
    {
        Assert.Contains(PolicyService.Descriptor.Methods, m => m.Name == rpcName);
    }

    [Fact]
    public void PolicyRevisionExposesCatalogFields()
    {
        Assert.Equal("address_objects", PolicyRevision.Descriptor.FindFieldByNumber(13)!.Name);
        Assert.Equal("service_objects", PolicyRevision.Descriptor.FindFieldByNumber(14)!.Name);
        Assert.Equal("chain_contracts", PolicyRevision.Descriptor.FindFieldByNumber(15)!.Name);
        Assert.Equal("tests_json", PolicyRevision.Descriptor.FindFieldByNumber(16)!.Name);
        Assert.NotNull(AddressObject.Descriptor.FindFieldByName("entries"));
        Assert.NotNull(ServiceObject.Descriptor.FindFieldByName("terms"));
        Assert.NotNull(ChainContract.Descriptor.FindFieldByName("default_disposition"));
        Assert.NotNull(PolicyRevisionDiff.Descriptor.FindFieldByName("risk_level"));
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

    [Fact]
    public void C1ComposeEffectivePolicyRequestIsNodeIdOnly()
    {
        Google.Protobuf.Reflection.MessageDescriptor descriptor = ComposeEffectivePolicyRequest.Descriptor;
        Assert.Equal("node_id", descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.Single(descriptor.Fields.InDeclarationOrder());
        Assert.DoesNotContain(
            descriptor.Fields.InDeclarationOrder(),
            static f => f.Name.Contains("vrrp", StringComparison.OrdinalIgnoreCase)
                        || f.Name.Contains("wan", StringComparison.OrdinalIgnoreCase)
                        || f.Name.Contains("device", StringComparison.OrdinalIgnoreCase)
                        || f.Name.Contains("binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void C4EffectivePolicyExposesRepeatedFindings()
    {
        Google.Protobuf.Reflection.FieldDescriptor findings = EffectivePolicy.Descriptor.FindFieldByName("findings");
        Assert.NotNull(findings);
        Assert.True(findings.IsRepeated);
        Assert.Equal("PolicyWarning", findings.MessageType.Name);
        Assert.NotNull(EffectivePolicy.Descriptor.FindFieldByName("logical_effective_hash"));
        Assert.NotNull(EffectivePolicy.Descriptor.FindFieldByName("company"));
        Assert.NotNull(PolicyRevisionRef.Descriptor.FindFieldByName("policy_id"));
        Assert.NotNull(PolicyRevisionRef.Descriptor.FindFieldByName("revision_id"));
        Assert.NotNull(PolicyRevisionRef.Descriptor.FindFieldByName("revision_number"));
        Assert.NotNull(PolicyRevisionRef.Descriptor.FindFieldByName("content_hash"));
    }

    [Fact]
    public void C1ExceptionMetadataAndUpdateRpcSurface()
    {
        Google.Protobuf.Reflection.MessageDescriptor metadata = ExceptionMetadata.Descriptor;
        string[] fields = metadata.Fields.InDeclarationOrder().Select(static f => f.Name).ToArray();
        Assert.Equal(
            [
                "target_scope",
                "target_scope_id",
                "target_stage",
                "waived_rule_id",
                "valid_from",
                "valid_until",
                "reason",
                "ticket_reference",
                "supersedes_exception_id",
            ],
            fields);
        Assert.Equal("exception_metadata", PolicyRevision.Descriptor.FindFieldByNumber(12)!.Name);
        Google.Protobuf.Reflection.MessageDescriptor request = UpdateExceptionMetadataRequest.Descriptor;
        Assert.Equal("idempotency_key", request.Fields.InDeclarationOrder()[0].Name);
        Assert.Equal("revision_id", request.Fields.InDeclarationOrder()[1].Name);
        Assert.Equal("expected_content_hash", request.Fields.InDeclarationOrder()[2].Name);
        Assert.Equal("metadata", request.Fields.InDeclarationOrder()[3].Name);
    }

    [Fact]
    public void ApprovalAndBindingRpcsExposeIdempotencyAndCas()
    {
        Assert.Equal("idempotency_key", RecordAnalysisRunRequest.Descriptor.Fields.InDeclarationOrder()[0].Name);
        Assert.Equal("expected_content_hash", RecordAnalysisRunRequest.Descriptor.FindFieldByName("expected_content_hash")!.Name);
        Assert.Equal("expected_bundle_hash", ApproveRevisionRequest.Descriptor.FindFieldByName("expected_bundle_hash")!.Name);
        Assert.Equal("current_dependency_fingerprint", ApproveRevisionRequest.Descriptor.FindFieldByName("current_dependency_fingerprint")!.Name);
        Assert.Equal("expected_row_version", ExpireExceptionBindingRequest.Descriptor.FindFieldByName("expected_row_version")!.Name);
        Assert.NotNull(PolicyBinding.Descriptor.FindFieldByName("deployment_started"));
        Assert.Equal("completes_approval", PolicyApprovalVote.Descriptor.FindFieldByName("completes_approval")!.Name);
    }
}
