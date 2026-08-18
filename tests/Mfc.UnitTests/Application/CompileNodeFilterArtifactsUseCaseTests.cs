using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using PolicyContainer = Mfc.Domain.Policy.Policy;

namespace Mfc.UnitTests.Application;

public sealed class CompileNodeFilterArtifactsUseCaseTests
{
    private static readonly byte[] ValidHash = Enumerable.Repeat((byte)0x11, 32).ToArray();
    private static readonly byte[] CapabilityHashBytes = H("cap").Bytes.ToArray();

    [Fact]
    public async Task UnauthorizedActorIsRejected()
    {
        CompileNodeFilterArtifactsUseCase useCase = CreateUseCase(out FakeAuthorizationBoundary auth, out _, out _, out _);
        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyWrite);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await useCase.ExecuteAsync(Command());
        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task InvalidFingerprintIsValidationError()
    {
        CompileNodeFilterArtifactsUseCase useCase = CreateUseCase(out _, out _, out _, out _);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await useCase.ExecuteAsync(Command(fingerprint: [1, 2, 3]));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public async Task InvalidCapabilityHashIsValidationError()
    {
        CompileNodeFilterArtifactsUseCase useCase = CreateUseCase(out _, out _, out _, out _);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await useCase.ExecuteAsync(Command(capability: [9]));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public async Task MissingNodeIsNotFound()
    {
        CompileNodeFilterArtifactsUseCase useCase = CreateUseCase(out _, out _, out _, out _);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await useCase.ExecuteAsync(Command());
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
    }

    [Fact]
    public async Task MissingAnalysisRunIsNotFound()
    {
        (CompileNodeFilterArtifactsUseCase useCase, Node node) = await CreateWithNodeAsync();
        ApplicationResult<CompileNodeFilterArtifactsView> result = await useCase.ExecuteAsync(Command(nodeId: node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.Contains("Analysis run", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedCompilerProfileIsRejected()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(withCapabilitySnapshot: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes,
            compilerProfile: Enumerable.Repeat((byte)0xAB, 32).ToArray()));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerProfileUnsupported, result.Error!.Code);
    }

    [Fact]
    public async Task StaleFingerprintBlocksCompilation()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(withCapabilitySnapshot: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: H("stale-fingerprint").Bytes.ToArray(),
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerAnalysisStale, result.Error!.Code);
    }

    [Fact]
    public async Task MissingCapabilityOnDeviceBlocksCompilation()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(withCapabilitySnapshot: false);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerCapabilityStale, result.Error!.Code);
        Assert.Empty(fx.Artifacts.Puts);
    }

    [Fact]
    public async Task LogicalEffectiveMismatchBlocksAsStale()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            logicalHash: H("wrong-logical"));
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerAnalysisStale, result.Error!.Code);
    }

    [Fact]
    public async Task EmptyChainContractsFailAfterGates()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(withCapabilitySnapshot: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, result.Error!.Code);
        Assert.Contains("Chain contracts", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulCompilePersistsContentAddressedArtifact()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fx.NodeId, result.Value!.NodeId);
        Assert.Single(result.Value.Artifacts);
        Assert.True(result.Value.Artifacts[0].StoredAsNew);
        Assert.Single(fx.Artifacts.Puts);

        ApplicationResult<CompileNodeFilterArtifactsView> again = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(again.IsSuccess, again.Error?.Message);
        Assert.False(again.Value!.Artifacts[0].StoredAsNew);
        Assert.Equal(2, fx.Artifacts.Puts.Count);
    }

    [Fact]
    public async Task CapabilityHashMismatchBlocksCompilation()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: H("other-cap").Bytes.ToArray()));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerCapabilityStale, result.Error!.Code);
    }

    [Fact]
    public async Task MissingDesiredBindingBlocksCompilation()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            skipBind: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, result.Error!.Code);
    }

    [Fact]
    public async Task SwitchNodeWithForwardContractsIsForbidden()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            nodeKind: NodeKind.Switch);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.SwitchForwardCompilationForbidden, result.Error!.Code);
        Assert.Empty(fx.Artifacts.Puts);
    }

    [Fact]
    public async Task InvalidCompilerProfileHashIsValidationError()
    {
        CompileNodeFilterArtifactsUseCase useCase = CreateUseCase(out _, out _, out _, out _);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await useCase.ExecuteAsync(Command(
            compilerProfile: [0x01, 0x02]));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public async Task ExplicitLayoutV1ProfileSucceeds()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes,
            compilerProfile: RouterOsCompilerProfile.LayoutV1Hash.Bytes.ToArray()));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!.Artifacts);
    }

    [Fact]
    public async Task NodeWithNoDevicesFailsClosed()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            addDevice: false);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, result.Error!.Code);
        Assert.Contains("no Devices", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyDisabledDevicesFailsClosed()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            enableDevice: false);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerInputNotApproved, result.Error!.Code);
        Assert.Contains("no enabled Devices", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrphanCaptureIdTreatsCapabilityAsStale()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            orphanCaptureId: true);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyCompilerCodes.CompilerCapabilityStale, result.Error!.Code);
    }

    [Fact]
    public async Task SiteScopedDesiredBindingAllowsCompilation()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            skipBind: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await fx.Approvals.AddBindingAsync(PolicyDesiredBinding.Reconstitute(
            PolicyBindingId.New(),
            PolicyBindingScope.Site,
            fx.SiteId,
            new PolicyId(fx.PolicyId),
            new PolicyRevisionId(fx.RevisionId),
            new PolicyAnalysisRunId(fx.RunId),
            Hash256.Create(fx.BundleHash),
            PolicyBindingState.Active,
            validFromUtc: null,
            validUntilUtc: null,
            rowVersion: 1,
            createdAtUtc: now,
            updatedAtUtc: now));
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!.Artifacts);
    }

    [Fact]
    public async Task NodeScopedDesiredBindingAllowsCompilation()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true,
            skipBind: true);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await fx.Approvals.AddBindingAsync(PolicyDesiredBinding.Reconstitute(
            PolicyBindingId.New(),
            PolicyBindingScope.Node,
            fx.NodeId,
            new PolicyId(fx.PolicyId),
            new PolicyRevisionId(fx.RevisionId),
            new PolicyAnalysisRunId(fx.RunId),
            Hash256.Create(fx.BundleHash),
            PolicyBindingState.Active,
            validFromUtc: null,
            validUntilUtc: null,
            rowVersion: 1,
            createdAtUtc: now,
            updatedAtUtc: now));
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!.Artifacts);
    }

    [Fact]
    public async Task MissingRevisionForAnalysisRunIsNotFound()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true);
        fx.Policies.RemoveRevision(new PolicyRevisionId(fx.RevisionId));
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.Contains("Policy revision", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingCompanyBaselineFailsCompose()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true);
        fx.Policies.RemovePolicy(new PolicyId(fx.PolicyId));
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.CompanyRequired, result.Error!.Code);
    }

    [Fact]
    public async Task DuplicateSiteOverlayFailsCompose()
    {
        CompileFixture fx = await SeedApprovedCompanyWithNodeDeviceAsync(
            withCapabilitySnapshot: true,
            withChainContracts: true);
        PolicyContainer overlayA = PolicyContainer.Create(
            NonEmptyName.Create("site-a"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            fx.SiteId);
        PolicyContainer overlayB = PolicyContainer.Create(
            NonEmptyName.Create("site-b"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            fx.SiteId);
        await fx.Policies.AddPolicyAsync(overlayA);
        await fx.Policies.AddPolicyAsync(overlayB);
        ApplicationResult<CompileNodeFilterArtifactsView> result = await fx.UseCase.ExecuteAsync(Command(
            nodeId: fx.NodeId,
            analysisRunId: fx.RunId,
            fingerprint: fx.Fingerprint,
            capability: CapabilityHashBytes));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.PolicyNotUnique, result.Error!.Code);
    }

    private static CompileNodeFilterArtifactsCommand Command(
        Guid? nodeId = null,
        Guid? analysisRunId = null,
        byte[]? fingerprint = null,
        byte[]? capability = null,
        byte[]? compilerProfile = null)
        => new()
        {
            Actor = "tester",
            NodeId = nodeId ?? Guid.NewGuid(),
            AnalysisRunId = analysisRunId ?? Guid.NewGuid(),
            CurrentDependencyFingerprint = fingerprint ?? ValidHash,
            CurrentCapabilityHash = capability ?? CapabilityHashBytes,
            CompilerProfileHash = compilerProfile,
        };

    private static CompileNodeFilterArtifactsUseCase CreateUseCase(
        out FakeAuthorizationBoundary auth,
        out FakeNodeStore nodes,
        out FakePolicyApprovalStore approvals,
        out FakeFilterArtifactStore artifacts)
    {
        (CompileNodeFilterArtifactsUseCase useCase, FakeNodeStore n, _, FakePolicyApprovalStore a, _) =
            CreateRaw(out auth, out artifacts);
        nodes = n;
        approvals = a;
        return useCase;
    }

    private static async Task<(CompileNodeFilterArtifactsUseCase UseCase, Node Node)> CreateWithNodeAsync()
    {
        (CompileNodeFilterArtifactsUseCase useCase, FakeNodeStore nodes, _, _, _) = CreateRaw();
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("N1"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        return (useCase, node);
    }

    private static async Task<CompileFixture> SeedApprovedCompanyWithNodeDeviceAsync(
        bool withCapabilitySnapshot,
        Hash256? logicalHash = null,
        bool withChainContracts = false,
        bool skipBind = false,
        bool addDevice = true,
        bool enableDevice = true,
        bool orphanCaptureId = false,
        NodeKind nodeKind = NodeKind.Router)
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        FakePolicyApprovalStore approvals = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeZoneDefinitionStore zones = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeZoneResolveObservationSource observations = new();
        FakeSnapshotStore snapshots = new();
        FakeFilterArtifactStore artifacts = new();

        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyDraftView> draft = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "author",
            IdempotencyKey = Guid.NewGuid(),
            Name = "baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
        Assert.True(draft.IsSuccess);
        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value!.RevisionId));
        Assert.NotNull(revision);

        if (withChainContracts)
        {
            ReplaceChainContractsUseCase replace = new(auth, policies, idempotency, audit);
            ApplicationResult<PolicyRevisionView> replaced = await replace.ExecuteAsync(new ReplaceChainContractsCommand
            {
                Actor = "author",
                IdempotencyKey = Guid.NewGuid(),
                RevisionId = revision!.Id.Value,
                ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
                Contracts =
                [
                    new ChainContractView
                    {
                        Family = IpAddressFamily.IPv4,
                        Chain = PolicyFilterChain.Forward,
                        DefaultDisposition = "DROP",
                    },
                ],
            });
            Assert.True(replaced.IsSuccess, replaced.Error?.Message);
            revision = await policies.GetRevisionAsync(revision.Id);
            Assert.NotNull(revision);
        }

        revision!.MarkValidated();
        await policies.SaveRevisionAsync(revision);

        SubmitRevisionForReviewUseCase submit = new(auth, policies, idempotency, audit);
        Assert.True((await submit.ExecuteAsync(new SubmitRevisionForReviewCommand
        {
            Actor = "author",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revision.Id.Value,
            ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
        })).IsSuccess);

        PolicyDocument document = PolicyDocumentReader.Read(revision.CanonicalBytes);
        Hash256 logical = logicalHash ?? ComposeLogical(revision, document);

        byte[] fingerprint = PolicyApprovalHasher.HashDependencyFingerprint(Vector()).Bytes.ToArray();
        RecordAnalysisRunUseCase record = new(auth, policies, approvals, idempotency, audit);
        ApplicationResult<PolicyAnalysisRunView> run = await record.ExecuteAsync(new RecordAnalysisRunCommand
        {
            Actor = "author",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revision.Id.Value,
            ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
            LogicalEffectiveHash = logical.Bytes.ToArray(),
            AnalysisContextHash = H("analysis").Bytes.ToArray(),
            EvidenceContextHash = H("evidence").Bytes.ToArray(),
            TopologyProjectionHash = H("topology").Bytes.ToArray(),
            ImpactSetHash = H("impact").Bytes.ToArray(),
            PerDeviceAnalysisHashes = [H("device").Bytes.ToArray()],
            DependencyFingerprint = fingerprint,
            RiskLevel = PolicyEvidenceAnalysisCodes.RiskLow,
            EvidenceSignalsPresent = true,
            AnalyzerVersion = PolicyApprovalCodes.AnalyzerVersion,
            PolicySchemaVersion = PolicyDocument.SchemaName,
            PipelineVersion = PolicyPipelineV1.Version,
            Findings = [],
            TestResults =
            [
                new PolicyApprovalTestInput
                {
                    TestId = Guid.NewGuid(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
        });
        Assert.True(run.IsSuccess, run.Error?.Message);

        ApproveRevisionUseCase approve = new(auth, policies, approvals, idempotency, audit, new FakeUnitOfWork());
        Assert.True((await approve.ExecuteAsync(new ApproveRevisionCommand
        {
            Actor = "reviewer",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revision.Id.Value,
            AnalysisRunId = run.Value!.Id,
            ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
            ExpectedBundleHash = Convert.FromHexString(run.Value.BundleHashHex),
            CurrentDependencyFingerprint = fingerprint,
        })).IsSuccess);

        if (!skipBind)
        {
            ActivateDesiredBindingUseCase bind = new(auth, policies, approvals, idempotency, audit, clock, new FakeUnitOfWork());
            Assert.True((await bind.ExecuteAsync(new ActivateDesiredBindingCommand
            {
                Actor = "binder",
                IdempotencyKey = Guid.NewGuid(),
                RevisionId = revision.Id.Value,
                AnalysisRunId = run.Value.Id,
                ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
                CurrentDependencyFingerprint = fingerprint,
            })).IsSuccess);
        }

        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("edge"), nodeKind, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        if (addDevice)
        {
            Device device = node.AddDevice(
                NonEmptyName.Create("r1"),
                ManagementEndpoint.Create("192.0.2.10"),
                DeviceRole.Router);
            if (withCapabilitySnapshot)
            {
                if (orphanCaptureId)
                {
                    device.RecordCompletedCapture(Guid.NewGuid());
                }
                else
                {
                    StoredSnapshot stored = new()
                    {
                        Metadata = SnapshotMetadata.CreateCompleted(
                            device.Id,
                            ConfigurationHash.FromDigest(H("cfg")),
                            ObservationHash.FromDigest(H("obs")),
                            CapabilityHash.FromDigest(H("cap")),
                            SnapshotHash.FromDigest(H("snap")),
                            DateTimeOffset.UtcNow),
                        SchemaVersion = 1,
                    };
                    await snapshots.AddAsync(stored);
                    device.RecordCompletedCapture(stored.Metadata.Id.Value);
                }
            }

            if (!enableDevice)
            {
                device.SetEnabled(false);
            }

            await devices.AddAsync(device);
            await nodes.UpdateAsync(node);
        }

        CompileNodeFilterArtifactsUseCase useCase = new(
            auth, nodes, devices, policies, approvals, zones, bindings, observations, snapshots, artifacts, clock);

        return new CompileFixture
        {
            UseCase = useCase,
            Artifacts = artifacts,
            Approvals = approvals,
            Policies = policies,
            NodeId = node.Id.Value,
            SiteId = node.SiteId.Value,
            PolicyId = draft.Value.PolicyId,
            RevisionId = revision.Id.Value,
            RunId = run.Value.Id,
            BundleHash = Convert.FromHexString(run.Value.BundleHashHex),
            Fingerprint = fingerprint,
        };
    }

    private static Hash256 ComposeLogical(PolicyRevision revision, PolicyDocument document)
    {
        PolicyLayer company = new()
        {
            PolicyId = revision.PolicyId.Value,
            RevisionId = revision.Id.Value,
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
            OwnerId = null,
            ContentHash = revision.ContentHash,
            ParentContextHash = null,
            PolicyDocument = document,
        };
        PolicyComposeResult composed = EffectivePolicyComposer.Compose(
            company,
            site: null,
            node: null,
            nodeId: Guid.NewGuid(),
            siteId: Guid.NewGuid(),
            knownZoneIds: new HashSet<Guid>(),
            exceptions: []);
        Assert.True(composed.IsSuccess, composed.Message);
        return composed.Value!.LogicalEffectiveHash;
    }

    private static (
        CompileNodeFilterArtifactsUseCase UseCase,
        FakeNodeStore Nodes,
        FakePolicyStore Policies,
        FakePolicyApprovalStore Approvals,
        FakeDeviceStore Devices)
        CreateRaw()
        => CreateRaw(out _, out _);

    private static (
        CompileNodeFilterArtifactsUseCase UseCase,
        FakeNodeStore Nodes,
        FakePolicyStore Policies,
        FakePolicyApprovalStore Approvals,
        FakeDeviceStore Devices)
        CreateRaw(out FakeAuthorizationBoundary auth, out FakeFilterArtifactStore artifacts)
    {
        auth = new FakeAuthorizationBoundary();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakePolicyStore policies = new();
        FakePolicyApprovalStore approvals = new();
        FakeZoneDefinitionStore zones = new();
        FakeNodeZoneBindingStore bindings = new();
        FakeZoneResolveObservationSource observations = new();
        FakeSnapshotStore snapshots = new();
        artifacts = new FakeFilterArtifactStore();
        FakeClock clock = new();
        CompileNodeFilterArtifactsUseCase useCase = new(
            auth, nodes, devices, policies, approvals, zones, bindings, observations, snapshots, artifacts, clock);
        return (useCase, nodes, policies, approvals, devices);
    }

    private static PolicyApprovalDependencyVector Vector()
        => new()
        {
            CompanyBindingHash = H("company"),
            SiteBindingHash = H("site"),
            NodeBindingHash = H("node"),
            ActiveExceptionsHash = H("exc"),
            ZoneBindingHash = H("zone"),
            NodeMembershipHash = H("members"),
            RouterOsConfigurationHash = H("ros"),
            CapabilityHash = H("cap"),
            CompatibilityHash = H("compat"),
            ManagementAccessProfileHash = H("mgmt"),
            AnchorGuardContextHash = H("anchor"),
            AnalyzerVersion = PolicyApprovalCodes.AnalyzerVersion,
            PolicySchemaVersion = PolicyDocument.SchemaName,
            PipelineVersion = PolicyPipelineV1.Version,
        };

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class CompileFixture
    {
        public required CompileNodeFilterArtifactsUseCase UseCase { get; init; }

        public required FakeFilterArtifactStore Artifacts { get; init; }

        public required FakePolicyApprovalStore Approvals { get; init; }

        public required FakePolicyStore Policies { get; init; }

        public required Guid NodeId { get; init; }

        public required Guid SiteId { get; init; }

        public required Guid PolicyId { get; init; }

        public required Guid RevisionId { get; init; }

        public required Guid RunId { get; init; }

        public required byte[] BundleHash { get; init; }

        public required byte[] Fingerprint { get; init; }
    }
}
