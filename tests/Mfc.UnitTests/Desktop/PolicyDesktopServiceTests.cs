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
        client.Revision = BuildDraftRevision(revisionId, ruleId);
        client.ListResponse = new ListRulesResponse
        {
            RevisionId = ToUuid(revisionId),
            ContentHash = client.Revision.ContentHash,
            Rules = { client.Revision.Rules },
        };

        PolicyPanelService service = new(client);
        PolicyRevisionPanelState state = await service.LoadRevisionAsync(revisionId);
        Assert.Single(state.Rules);
        Assert.Equal(0u, state.Rules[0].Ordinal);
        Assert.Contains("Accept", state.Rules[0].EffectText, StringComparison.Ordinal);
        Assert.Contains("POLICY_SELECTOR_CATALOG_SOFT", state.Rules[0].WarningLines[0], StringComparison.Ordinal);
        Assert.Contains("allow-lan", state.Rules[0].SummaryLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4ParseAddressEntriesRejectsRawMatcherAndAcceptsHostCidrRange()
    {
        IReadOnlyList<AddressObjectEntry> entries = PolicyPanelService.ParseAddressEntries(
            "10.0.0.1\n10.0.0.0/24\n10.0.0.5-10.0.0.10");
        Assert.Equal(3, entries.Count);
        Assert.Equal("HOST", entries[0].Kind);
        Assert.Equal("PREFIX", entries[1].Kind);
        Assert.Equal(24u, entries[1].PrefixLength);
        Assert.Equal("RANGE", entries[2].Kind);
        Assert.Throws<ArgumentException>(() => PolicyPanelService.ParseAddressEntries("in-interface=ether1"));
    }

    [Fact]
    public async Task Ac1EditorsUpsertAddressServiceContractsAndTests()
    {
        FakePolicyServiceClient client = new();
        Guid revisionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        client.Revision = BuildDraftRevision(revisionId, ruleId: null);
        PolicyPanelService service = new(client);
        byte[] hash = client.Revision.ContentHash.Value.ToByteArray();

        PolicyRevisionPanelState afterAddress = await service.UpsertAddressObjectAsync(
            revisionId, hash, "lan", IpAddressFamily.Ipv4, "10.0.0.0/8");
        Assert.Contains(afterAddress.AddressObjects, a => a.Name == "lan");

        PolicyRevisionPanelState afterService = await service.UpsertTcpServiceObjectAsync(
            revisionId, afterAddress.ContentHash, "https", 443);
        Assert.Contains(afterService.ServiceObjects, s => s.Name == "https" && s.TermsText.Contains("443", StringComparison.Ordinal));

        PolicyRevisionPanelState afterContracts = await service.ReplaceChainContractsAsync(
            revisionId, afterService.ContentHash, IpAddressFamily.Ipv4, PolicyFilterChain.Forward, "DROP");
        Assert.Contains(afterContracts.ChainContracts, c => c.Disposition == "DROP");

        PolicyRevisionPanelState afterInput = await service.ReplaceChainContractsAsync(
            revisionId, afterContracts.ContentHash, IpAddressFamily.Ipv4, PolicyFilterChain.Input, "DROP");
        Assert.Equal(2, afterInput.ChainContracts.Count);
        Assert.Contains(afterInput.ChainContracts, c => c.ChainText.Contains("Forward", StringComparison.Ordinal));
        Assert.Contains(afterInput.ChainContracts, c => c.ChainText.Contains("Input", StringComparison.Ordinal));

        PolicyRevisionPanelState afterReject = await service.ReplaceChainContractsAsync(
            revisionId,
            afterInput.ContentHash,
            IpAddressFamily.Ipv4,
            PolicyFilterChain.Forward,
            "REJECT",
            RejectMode.TcpReset);
        Assert.Equal(2, afterReject.ChainContracts.Count);
        Assert.Contains(
            afterReject.ChainContracts,
            c => c.Disposition == "REJECT" && c.SummaryLine.Contains("TcpReset", StringComparison.Ordinal));
        Assert.Contains(afterReject.ChainContracts, c => c.ChainText.Contains("Input", StringComparison.Ordinal));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceChainContractsAsync(
            revisionId,
            afterReject.ContentHash,
            IpAddressFamily.Ipv4,
            PolicyFilterChain.Forward,
            "REJECT",
            RejectMode.Unspecified));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceChainContractsAsync(
            revisionId,
            afterReject.ContentHash,
            IpAddressFamily.Ipv4,
            PolicyFilterChain.Forward,
            "REJECT",
            rejectMode: null));

        PolicyRevisionPanelState afterTests = await service.ReplaceTestsAsync(
            revisionId, afterReject.ContentHash, "[{\"id\":\"t1\"}]");
        Assert.Contains("t1", afterTests.TestsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac2Ac3ReorderRejectsCrossStageAndAcceptsSameStagePermutation()
    {
        FakePolicyServiceClient client = new();
        Guid revisionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid ruleA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid ruleB = Guid.Parse("22222222-3333-4444-5555-666666666666");
        Guid otherStage = Guid.Parse("33333333-4444-5555-6666-777777777777");
        client.Revision = BuildDraftRevision(revisionId, ruleA);
        client.Revision.Rules.Add(BuildRule(ruleB, PolicyPipelineStage.CompanyAllow, ordinal: 1));
        client.Revision.Rules.Add(BuildRule(otherStage, PolicyPipelineStage.CompanyDeny, ordinal: 0));
        PolicyPanelService service = new(client);
        byte[] hash = client.Revision.ContentHash.Value.ToByteArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReorderRulesInStageAsync(
            revisionId,
            hash,
            IpAddressFamily.Ipv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            [ruleA, otherStage]));

        PolicyRevisionPanelState reordered = await service.ReorderRulesInStageAsync(
            revisionId,
            hash,
            IpAddressFamily.Ipv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            [ruleB, ruleA]);
        Assert.Equal(2, reordered.Rules.Count(r => r.Stage == PolicyPipelineStage.CompanyAllow));
        Assert.True(client.LastReorderFamily == IpAddressFamily.Ipv4
                    && client.LastReorderChain == PolicyFilterChain.Forward
                    && client.LastReorderStage == PolicyPipelineStage.CompanyAllow);
        Assert.Equal([ruleB, ruleA], client.LastReorderIds);
    }

    [Fact]
    public async Task Ac6Ac7Ac8ComposeDiffAndAnalysisRiskSurfaces()
    {
        FakePolicyServiceClient client = new();
        Guid revisionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid baselineId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid nodeId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        client.Revision = BuildDraftRevision(revisionId, ruleId: null);
        client.ComposeResponse = new EffectivePolicy
        {
            NodeId = ToUuid(nodeId),
            LogicalEffectiveHash = HashBytes(7),
            Findings =
            {
                new PolicyWarning { Code = "RULE_EMPTY", Message = "empty selector", Subject = "rule-1" },
            },
        };
        client.DiffResponse = new PolicyRevisionDiff
        {
            BeforeRevisionId = ToUuid(baselineId),
            AfterRevisionId = ToUuid(revisionId),
            RiskLevel = "HIGH",
            SemanticClasses = { "ADDED" },
            PacketSpaceClasses = { "NEWLY_ACCEPTED" },
            RiskDrivers = { "ADD_ALLOW" },
            RuleChanges =
            {
                new PolicyRuleDiffLine
                {
                    RuleId = ToUuid(Guid.Parse("11111111-2222-3333-4444-555555555555")),
                    Changes = { "ADDED" },
                },
            },
        };
        client.AnalysisRunResponse = new PolicyAnalysisRun
        {
            Id = ToUuid(Guid.Parse("44444444-5555-6666-7777-888888888888")),
            RevisionId = ToUuid(revisionId),
            BundleHash = HashBytes(8),
            DependencyFingerprint = HashBytes(9),
            RiskLevel = "HIGH",
            EffectiveRiskLevel = "CRITICAL",
        };

        PolicyPanelService service = new(client);
        PolicyComposePanelResult compose = await service.ComposeAsync(nodeId);
        Assert.Contains(compose.Findings, f => f.SummaryLine.Contains("RULE_EMPTY", StringComparison.Ordinal));

        PolicyDiffPanelResult diff = await service.DiffAsync(baselineId, revisionId);
        Assert.Equal("HIGH", diff.RiskLevel);
        Assert.Contains(diff.Lines, l => l.SummaryLine.Contains("ADDED", StringComparison.Ordinal));

        PolicyAnalysisRunListItem run = await service.RecordAnalysisRunAsync(
            revisionId,
            client.Revision.ContentHash.Value.ToByteArray(),
            compose.LogicalEffectiveHash,
            "HIGH",
            compose.Findings);
        Assert.Equal("HIGH", run.RiskLevel);
        Assert.Equal("CRITICAL", run.EffectiveRiskLevel);
    }

    [Fact]
    public async Task Ac11ApprovedRevisionIsReadOnly()
    {
        FakePolicyServiceClient client = new();
        Guid revisionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        client.Revision = BuildDraftRevision(revisionId, ruleId: null);
        client.Revision.State = PolicyRevisionState.Approved;
        PolicyPanelService service = new(client);
        PolicyRevisionPanelState state = await service.LoadRevisionAsync(revisionId);
        Assert.True(state.IsReadOnly);
        Assert.Equal(PolicyRevisionState.Approved, state.State);
    }

    [Fact]
    public void Ac9Ac10SeparateActionsAndNoSaveAndDeploySurface()
    {
        Type vm = typeof(Mfc.Desktop.ViewModels.PoliciesViewModel);
        Assert.NotNull(vm.GetProperty("ValidateCommand"));
        Assert.NotNull(vm.GetProperty("SubmitCommand"));
        Assert.NotNull(vm.GetProperty("ApproveCommand"));
        Assert.NotNull(vm.GetProperty("BindCommand"));
        Assert.NotNull(vm.GetProperty("DeployCommand"));
        Assert.NotNull(vm.GetProperty("CreateDraftCommand"));
        Assert.Null(vm.GetProperty("SaveAndDeployCommand"));
        Assert.DoesNotContain(
            typeof(Mfc.Desktop.ViewModels.PoliciesViewModel).GetMethods(),
            m => m.Name.Contains("SaveAndDeploy", StringComparison.Ordinal));
    }

    private static PolicyRevision BuildDraftRevision(Guid revisionId, Guid? ruleId)
    {
        PolicyRevision revision = new()
        {
            Id = ToUuid(revisionId),
            PolicyId = ToUuid(Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000")),
            RevisionNumber = 1,
            SchemaVersion = 1,
            State = PolicyRevisionState.Draft,
            ContentHash = HashBytes(1),
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
            TestsJson = "[]",
        };
        if (ruleId is Guid id)
        {
            revision.Rules.Add(BuildRule(id, PolicyPipelineStage.CompanyAllow, ordinal: 0));
        }

        return revision;
    }

    private static PolicyRule BuildRule(Guid ruleId, PolicyPipelineStage stage, uint ordinal)
        => new()
        {
            Id = ToUuid(ruleId),
            Family = IpAddressFamily.Ipv4,
            Chain = PolicyFilterChain.Forward,
            Stage = stage,
            Ordinal = ordinal,
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
        };

    private static Uuid ToUuid(Guid value)
        => new() { Value = ByteString.CopyFrom(value.ToByteArray(bigEndian: true)) };

    private static Sha256 HashBytes(byte fill)
        => new() { Value = ByteString.CopyFrom(Enumerable.Repeat(fill, 32).ToArray()) };

    private sealed class FakePolicyServiceClient : IPolicyServiceClient
    {
        public PolicyRevision Revision { get; set; } = new();
        public ListRulesResponse ListResponse { get; set; } = new();
        public EffectivePolicy ComposeResponse { get; set; } = new();
        public PolicyRevisionDiff DiffResponse { get; set; } = new();
        public PolicyAnalysisRun AnalysisRunResponse { get; set; } = new();
        public IpAddressFamily? LastReorderFamily { get; private set; }
        public PolicyFilterChain? LastReorderChain { get; private set; }
        public PolicyPipelineStage? LastReorderStage { get; private set; }
        public IReadOnlyList<Guid>? LastReorderIds { get; private set; }

        public Task<PolicyDraft> CreateDraftPolicyAsync(
            string name,
            PolicyKind kind,
            PolicyOwnerScope ownerScope,
            Guid? ownerId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PolicyDraft
            {
                PolicyId = Revision.PolicyId,
                RevisionId = Revision.Id,
                Name = name,
                Kind = kind,
                OwnerScope = ownerScope,
                RevisionNumber = 1,
                ContentHash = Revision.ContentHash,
            });

        public Task<PolicyRevision> GetPolicyRevisionAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CloneRevision(Revision));

        public Task<ListRulesResponse> ListRulesAsync(
            Guid revisionId,
            bool activeOnly = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ListResponse);

        public Task<PolicyRuleMutation> AddRuleAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            IpAddressFamily family,
            PolicyFilterChain chain,
            PolicyPipelineStage stage,
            uint ordinal,
            bool enabled,
            TrafficPredicate? predicate,
            RuleEffect effect,
            string description,
            CancellationToken cancellationToken = default)
        {
            BumpHash();
            Revision.Rules.Add(new PolicyRule
            {
                Id = ToUuid(Guid.NewGuid()),
                Family = family,
                Chain = chain,
                Stage = stage,
                Ordinal = (uint)Revision.Rules.Count,
                Enabled = enabled,
                Effect = effect,
                Description = description,
                Predicate = predicate,
            });
            return Task.FromResult(new PolicyRuleMutation { ContentHash = Revision.ContentHash });
        }

        public Task<PolicyRuleMutation> ReorderRulesAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            IpAddressFamily family,
            PolicyFilterChain chain,
            PolicyPipelineStage stage,
            IReadOnlyList<Guid> orderedRuleIds,
            CancellationToken cancellationToken = default)
        {
            LastReorderFamily = family;
            LastReorderChain = chain;
            LastReorderStage = stage;
            LastReorderIds = orderedRuleIds.ToArray();
            BumpHash();
            return Task.FromResult(new PolicyRuleMutation { ContentHash = Revision.ContentHash });
        }

        public Task<PolicyRevision> ValidateRevisionAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            CancellationToken cancellationToken = default)
        {
            Revision.State = PolicyRevisionState.Validated;
            BumpHash();
            return Task.FromResult(CloneRevision(Revision));
        }

        public Task<PolicyRevision> UpsertAddressObjectAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            Guid? objectId,
            string name,
            IpAddressFamily family,
            IReadOnlyList<AddressObjectEntry> entries,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            AddressObject obj = new()
            {
                Id = ToUuid(objectId ?? Guid.NewGuid()),
                Name = name,
                Family = family,
            };
            obj.Entries.AddRange(entries);
            Revision.AddressObjects.Clear();
            Revision.AddressObjects.Add(obj);
            BumpHash();
            return Task.FromResult(CloneRevision(Revision));
        }

        public Task<PolicyRevision> UpsertServiceObjectAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            Guid? objectId,
            string name,
            IReadOnlyList<ServiceTerm> terms,
            string? description = null,
            CancellationToken cancellationToken = default)
        {
            ServiceObject obj = new()
            {
                Id = ToUuid(objectId ?? Guid.NewGuid()),
                Name = name,
            };
            obj.Terms.AddRange(terms);
            Revision.ServiceObjects.Clear();
            Revision.ServiceObjects.Add(obj);
            BumpHash();
            return Task.FromResult(CloneRevision(Revision));
        }

        public Task<PolicyRevision> ReplaceChainContractsAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            IReadOnlyList<ChainContract> contracts,
            CancellationToken cancellationToken = default)
        {
            Revision.ChainContracts.Clear();
            Revision.ChainContracts.AddRange(contracts);
            BumpHash();
            return Task.FromResult(CloneRevision(Revision));
        }

        public Task<PolicyRevision> ReplacePolicyTestsAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            string? testsJson,
            CancellationToken cancellationToken = default)
        {
            Revision.TestsJson = testsJson ?? "[]";
            BumpHash();
            return Task.FromResult(CloneRevision(Revision));
        }

        public Task<PolicyRevisionDiff> DiffPolicyRevisionsAsync(
            Guid beforeRevisionId,
            Guid afterRevisionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DiffResponse);

        public Task<EffectivePolicy> ComposeEffectivePolicyAsync(
            Guid nodeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ComposeResponse);

        public Task<PolicyRevision> SubmitRevisionForReviewAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            CancellationToken cancellationToken = default)
        {
            Revision.State = PolicyRevisionState.InReview;
            return Task.FromResult(CloneRevision(Revision));
        }

        public Task<PolicyAnalysisRun> RecordAnalysisRunAsync(
            Guid revisionId,
            byte[] expectedContentHash,
            byte[] logicalEffectiveHash,
            byte[] analysisContextHash,
            byte[] evidenceContextHash,
            byte[] topologyProjectionHash,
            byte[] impactSetHash,
            IReadOnlyList<byte[]> perDeviceAnalysisHashes,
            byte[] dependencyFingerprint,
            string riskLevel,
            bool evidenceSignalsPresent,
            string analyzerVersion,
            string policySchemaVersion,
            string pipelineVersion,
            IReadOnlyList<PolicyAnalysisFinding>? findings = null,
            IReadOnlyList<PolicyAnalysisTestResult>? testResults = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(AnalysisRunResponse);

        public Task<PolicyApprovalVote> ApproveRevisionAsync(
            Guid revisionId,
            Guid analysisRunId,
            byte[] expectedContentHash,
            byte[] expectedBundleHash,
            byte[] currentDependencyFingerprint,
            CancellationToken cancellationToken = default)
        {
            Revision.State = PolicyRevisionState.Approved;
            return Task.FromResult(new PolicyApprovalVote
            {
                ApprovalId = ToUuid(Guid.NewGuid()),
                RevisionId = ToUuid(revisionId),
                RevisionState = PolicyRevisionState.Approved,
                CompletesApproval = true,
                BundleHash = HashBytes(8),
            });
        }

        public Task<PolicyBinding> ActivateDesiredBindingAsync(
            Guid revisionId,
            Guid analysisRunId,
            byte[] expectedContentHash,
            byte[] currentDependencyFingerprint,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PolicyBinding
            {
                Id = ToUuid(Guid.NewGuid()),
                Scope = PolicyBindingScope.Company,
                PolicyId = Revision.PolicyId,
                DesiredRevisionId = ToUuid(revisionId),
                State = PolicyBindingState.Active,
                RowVersion = 1,
                DeploymentStarted = false,
            });

        private void BumpHash()
        {
            byte fill = (byte)((Revision.ContentHash.Value[0] + 1) % 255);
            if (fill == 0)
            {
                fill = 1;
            }

            Revision.ContentHash = HashBytes(fill);
        }

        private static PolicyRevision CloneRevision(PolicyRevision source)
            => PolicyRevision.Parser.ParseFrom(source.ToByteArray());
    }
}
