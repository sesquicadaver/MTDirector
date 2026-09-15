using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Topology;
using Mfc.Domain;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec — AUDIT-GUI-01 sealed CreatePlanFromSealedArtifacts:
/// <see cref="SealedDeploymentPlanBuilder"/> + <see cref="CreateDeploymentPlanFromSealedArtifactsUseCase"/>.
/// </summary>
public sealed class SealedDeploymentPlanFromArtifactsLivingSpecTests
{
    private static readonly Hash256 ProfileHash =
        Hash256.ParseHex("1111111111111111111111111111111111111111111111111111111111111111");

    private static readonly Hash256 SemanticsHash =
        Hash256.ParseHex("2222222222222222222222222222222222222222222222222222222222222222");

    private static readonly Hash256 AltSemanticsHash =
        Hash256.ParseHex("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

    [Fact]
    public void BuilderUsesBootstrapOldAnchorsWhenHashStateIsNull()
    {
        DeviceId deviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        RouterOsFilterArtifact sealedArtifact = CreateArtifact(deviceId);
        RouterOsFilterArtifactReader.ParsedBody newBody =
            RouterOsFilterArtifactReader.Read(sealedArtifact.CanonicalBytes.ToArray());
        ConfigurationHash cfg = ConfigurationHash.FromDigest(DeploymentTestFactory.H("cfg"));

        DeviceDeploymentPlan plan = SealedDeploymentPlanBuilder.Build(
            deviceId,
            "7.16.2",
            ToMeta(sealedArtifact),
            newBody,
            hashState: null,
            oldBody: null,
            cfg);

        Assert.Equal(BootstrapArtifact.Hash.ToString(), plan.OldArtifactHash.ToString());
        Assert.Equal(sealedArtifact.ResourceHash.ToString(), plan.NewArtifactHash.ToString());
        Assert.Equal(
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Forward),
            Assert.Single(plan.OldAnchorTargets).JumpTarget);
        Assert.Equal(newBody.Anchors[0].DesiredJumpTarget, Assert.Single(plan.NewAnchorTargets).JumpTarget);
        Assert.Equal("7.16.2", plan.ExpectedRouterOsVersion);
    }

    [Fact]
    public void BuilderUsesOldBodyWhenLastCommittedPresent()
    {
        DeviceId deviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        RouterOsFilterArtifact oldArtifact = CreateArtifact(deviceId, AltSemanticsHash);
        RouterOsFilterArtifact newArtifact = CreateArtifact(deviceId, SemanticsHash);
        RouterOsFilterArtifactReader.ParsedBody oldBody =
            RouterOsFilterArtifactReader.Read(oldArtifact.CanonicalBytes.ToArray());
        RouterOsFilterArtifactReader.ParsedBody newBody =
            RouterOsFilterArtifactReader.Read(newArtifact.CanonicalBytes.ToArray());
        DeviceHashState hashState = DeviceHashState.Create(
            deviceId,
            desiredPolicyHash: null,
            desiredArtifactHash: null,
            lastCommittedPolicyHash: null,
            lastCommittedArtifactHash: oldArtifact.ResourceHash,
            actualManagedResourceHash: null,
            actualKnown: false,
            anchorKnown: false,
            updatedAtUtc: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));

        DeviceDeploymentPlan plan = SealedDeploymentPlanBuilder.Build(
            deviceId,
            "7.16.2",
            ToMeta(newArtifact),
            newBody,
            hashState,
            oldBody,
            configurationHash: null);

        Assert.Equal(oldArtifact.ResourceHash.ToString(), plan.OldArtifactHash.ToString());
        Assert.Equal(oldBody.Anchors[0].DesiredJumpTarget, Assert.Single(plan.OldAnchorTargets).JumpTarget);
        Assert.Equal(newBody.Anchors[0].DesiredJumpTarget, Assert.Single(plan.NewAnchorTargets).JumpTarget);
    }

    [Fact]
    public void BuilderFailsWhenAnchorsEmpty()
    {
        DeviceId deviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        RouterOsFilterArtifact sealedArtifact = CreateArtifact(deviceId);
        RouterOsFilterArtifactReader.ParsedBody empty = new()
        {
            LayoutVersion = "1",
            ArtifactId = sealedArtifact.ArtifactId,
            AddressLists = [],
            Chains = [],
            Anchors = [],
        };

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            SealedDeploymentPlanBuilder.Build(
                deviceId,
                "7.16.2",
                ToMeta(sealedArtifact),
                empty,
                hashState: null,
                oldBody: null,
                configurationHash: null));
        Assert.Contains(DeploymentCodes.AnchorInvalid, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderFailsWhenDeviceIdMismatchesArtifact()
    {
        DeviceId artifactDevice = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        DeviceId otherDevice = new(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));
        RouterOsFilterArtifact sealedArtifact = CreateArtifact(artifactDevice);
        RouterOsFilterArtifactReader.ParsedBody newBody =
            RouterOsFilterArtifactReader.Read(sealedArtifact.CanonicalBytes.ToArray());

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            SealedDeploymentPlanBuilder.Build(
                otherDevice,
                "7.16.2",
                ToMeta(sealedArtifact),
                newBody,
                hashState: null,
                oldBody: null,
                configurationHash: null));
        Assert.Contains(DeploymentCodes.DevicePlanCardinality, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderFailsWhenOldAnchorsDoNotCoverNewKeys()
    {
        DeviceId deviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        RouterOsFilterArtifact newArtifact = CreateArtifact(deviceId, SemanticsHash, FilterBuiltInContext.Forward);
        RouterOsFilterArtifact oldArtifact = CreateArtifact(deviceId, AltSemanticsHash, FilterBuiltInContext.Input);
        RouterOsFilterArtifactReader.ParsedBody newBody =
            RouterOsFilterArtifactReader.Read(newArtifact.CanonicalBytes.ToArray());
        RouterOsFilterArtifactReader.ParsedBody oldBody =
            RouterOsFilterArtifactReader.Read(oldArtifact.CanonicalBytes.ToArray());

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            SealedDeploymentPlanBuilder.Build(
                deviceId,
                "7.16.2",
                ToMeta(newArtifact),
                newBody,
                hashState: null,
                oldBody,
                configurationHash: null));
        Assert.Contains(DeploymentCodes.AnchorInvalid, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteDeniesUnauthorizedActor()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        harness.Auth.DeniedPermissions.Add(ApplicationPermissions.DeploymentWrite);

        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteFailsWhenDevicesListEmpty()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(devices: []));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("at least one sealed device artifact", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenNodeNotFound()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(nodeId: Guid.NewGuid()));
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.Contains("Node", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenAnalysisRunNotFound()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(analysisRunId: Guid.NewGuid()));
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.Contains("Analysis run", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenDeviceIsNotOnNode()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(deviceId: Guid.NewGuid()));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("is not a member of node", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenNewArtifactHashIsInvalid()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(newHash: [1, 2, 3]));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("new_artifact_resource_hash", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenArtifactMetadataMissing()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(newHash: DeploymentTestFactory.H("missing-meta").Bytes.ToArray()));
        Assert.True(result.IsFailure);
        Assert.Contains(DeploymentCodes.ActiveArtifactHashMismatch, result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("metadata missing", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenArtifactBodyMissing()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        string key = harness.NewArtifact.ResourceHash.ToString();
        harness.Artifacts.CanonicalBytesByHash.Remove(key);

        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsFailure);
        Assert.Contains(DeploymentCodes.ActiveArtifactHashMismatch, result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("body missing", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenArtifactBodyEmpty()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        harness.Artifacts.CanonicalBytesByHash[harness.NewArtifact.ResourceHash.ToString()] = [];

        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsFailure);
        Assert.Contains("body missing", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenLastCaptureLacksRouterOsVersion()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync(seedCapture: false);
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("lacks RouterOS version", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenCaptureExistsWithoutVersionSection()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync(seedCapture: true, includeRouterOsVersion: false);
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsFailure);
        Assert.Contains("lacks RouterOS version", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteMapsDomainInvariantFromCorruptCanonicalBody()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        harness.Artifacts.CanonicalBytesByHash[harness.NewArtifact.ResourceHash.ToString()] = "{}"u8.ToArray();

        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains(RouterOsFilterArtifactReader.UnsupportedShapeCode, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteFailsWhenCreatePlanIdempotencyKeyIsEmpty()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(
            harness.Command(idempotencyKey: Guid.Empty));
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteCreatesPlanWithEmptyPacketPathPairs()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync();
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEqual(Guid.Empty, result.Value!.Plan.PlanId);
        Assert.Equal(harness.Node.Id.Value, result.Value.Plan.NodeId);
        Assert.Empty(result.Value.CapturePacketPathPairs);
        Assert.Equal(
            harness.NewArtifact.ResourceHash.ToString(),
            Convert.ToHexString(result.Value.Plan.Devices[0].NewArtifactHash),
            ignoreCase: true);
        Assert.Equal(
            BootstrapArtifact.Hash.ToString(),
            Convert.ToHexString(result.Value.Plan.Devices[0].OldArtifactHash),
            ignoreCase: true);
    }

    [Fact]
    public async Task ExecuteCreatesPlanFromLastCommittedOldBodyAndDeduplicatesPairs()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync(
            seedOldArtifact: true,
            includePacketPathPairs: true);
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            harness.OldArtifact!.ResourceHash.ToString(),
            Convert.ToHexString(result.Value!.Plan.Devices[0].OldArtifactHash),
            ignoreCase: true);
        PacketPathPairFact pair = Assert.Single(result.Value.CapturePacketPathPairs);
        Assert.Equal("ether1", pair.IngressInterface);
        Assert.Equal("wan1", pair.EgressInterface);
        Assert.Equal(PacketPathKind.CpuFirewallPath, pair.PathClass);
    }

    [Fact]
    public async Task ExecuteTreatsBootstrapLastCommittedAsNoOldBody()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync(seedBootstrapHashState: true);
        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            BootstrapArtifact.Hash.ToString(),
            Convert.ToHexString(result.Value!.Plan.Devices[0].OldArtifactHash),
            ignoreCase: true);
    }

    [Fact]
    public async Task ExecuteFallsBackToBootstrapWhenOldCanonicalMissing()
    {
        SealedPlanHarness harness = await SealedPlanHarness.CreateAsync(seedOldArtifact: true);
        harness.Artifacts.CanonicalBytesByHash.Remove(harness.OldArtifact!.ResourceHash.ToString());

        ApplicationResult<SealedDeploymentPlanResult> result = await harness.Sut.ExecuteAsync(harness.Command());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            harness.OldArtifact.ResourceHash.ToString(),
            Convert.ToHexString(result.Value!.Plan.Devices[0].OldArtifactHash),
            ignoreCase: true);
    }

    private sealed class SealedPlanHarness
    {
        private SealedPlanHarness(
            Node node,
            Device device,
            PolicyAnalysisRun run,
            RouterOsFilterArtifact newArtifact,
            RouterOsFilterArtifact? oldArtifact,
            FakeAuthorizationBoundary auth,
            FakeFilterArtifactStore artifacts,
            CreateDeploymentPlanFromSealedArtifactsUseCase sut)
        {
            Node = node;
            Device = device;
            Run = run;
            NewArtifact = newArtifact;
            OldArtifact = oldArtifact;
            Auth = auth;
            Artifacts = artifacts;
            Sut = sut;
        }

        public Node Node { get; }

        public Device Device { get; }

        public PolicyAnalysisRun Run { get; }

        public RouterOsFilterArtifact NewArtifact { get; }

        public RouterOsFilterArtifact? OldArtifact { get; }

        public FakeAuthorizationBoundary Auth { get; }

        public FakeFilterArtifactStore Artifacts { get; }

        public CreateDeploymentPlanFromSealedArtifactsUseCase Sut { get; }

        public static async Task<SealedPlanHarness> CreateAsync(
            bool seedCapture = true,
            bool includeRouterOsVersion = true,
            bool includePacketPathPairs = false,
            bool seedOldArtifact = false,
            bool seedBootstrapHashState = false)
        {
            Node node = DeploymentTestFactory.RouterWithDevice(out Device device);
            FakeNodeStore nodes = new();
            await nodes.AddAsync(node);
            FakeDeviceStore devices = new();
            await devices.AddAsync(device);

            FakeAuthorizationBoundary auth = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new();
            FakeDeploymentStore deployments = new();
            FakeSnapshotStore snapshots = new();
            FakeDeviceHashStateStore hashStates = new();
            FakeFilterArtifactStore artifacts = new();
            FakePolicyApprovalStore approvals = new();

            PolicyAnalysisRun run = CreateAnalysisRun();
            await approvals.AddAnalysisRunAsync(run);

            RouterOsFilterArtifact newArtifact = CreateArtifact(device.Id, SemanticsHash);
            await artifacts.PutIfAbsentAsync(newArtifact, Provenance(device.Id), CancellationToken.None);

            RouterOsFilterArtifact? oldArtifact = null;
            if (seedOldArtifact)
            {
                oldArtifact = CreateArtifact(device.Id, AltSemanticsHash);
                await artifacts.PutIfAbsentAsync(oldArtifact, Provenance(device.Id), CancellationToken.None);
                await hashStates.UpsertAsync(DeviceHashState.Create(
                    device.Id,
                    desiredPolicyHash: null,
                    desiredArtifactHash: null,
                    lastCommittedPolicyHash: null,
                    lastCommittedArtifactHash: oldArtifact.ResourceHash,
                    actualManagedResourceHash: null,
                    actualKnown: false,
                    anchorKnown: false,
                    updatedAtUtc: clock.UtcNow));
            }
            else if (seedBootstrapHashState)
            {
                await hashStates.UpsertAsync(DeviceHashState.Create(
                    device.Id,
                    desiredPolicyHash: null,
                    desiredArtifactHash: null,
                    lastCommittedPolicyHash: null,
                    lastCommittedArtifactHash: BootstrapArtifact.Hash,
                    actualManagedResourceHash: null,
                    actualKnown: false,
                    anchorKnown: false,
                    updatedAtUtc: clock.UtcNow));
            }

            if (seedCapture)
            {
                StoredSnapshot stored = new()
                {
                    Metadata = SnapshotMetadata.CreateCompleted(
                        device.Id,
                        ConfigurationHash.FromDigest(DeploymentTestFactory.H("cfg")),
                        ObservationHash.FromDigest(DeploymentTestFactory.H("obs")),
                        CapabilityHash.FromDigest(DeploymentTestFactory.H("cap")),
                        SnapshotHash.FromDigest(DeploymentTestFactory.H("snap")),
                        clock.UtcNow),
                    SchemaVersion = 1,
                };
                await snapshots.AddAsync(stored);
                snapshots.SectionsBySnapshot[stored.Metadata.Id.Value] = CaptureSections(
                    includeRouterOsVersion,
                    includePacketPathPairs);
                device.RecordCompletedCapture(stored.Metadata.Id.Value);
                await devices.UpdateAsync(device);
            }

            CreateDeploymentPlanUseCase createPlan = new(
                auth,
                nodes,
                deployments,
                idempotency,
                audit,
                clock,
                new VrrpPairConsistencyLoader(devices, snapshots, hashStates),
                new FakeUnitOfWork());

            return new SealedPlanHarness(
                node,
                device,
                run,
                newArtifact,
                oldArtifact,
                auth,
                artifacts,
                new CreateDeploymentPlanFromSealedArtifactsUseCase(
                    auth,
                    nodes,
                    devices,
                    approvals,
                    artifacts,
                    hashStates,
                    snapshots,
                    createPlan));
        }

        public CreateDeploymentPlanFromSealedArtifactsCommand Command(
            Guid? nodeId = null,
            Guid? analysisRunId = null,
            Guid? idempotencyKey = null,
            Guid? deviceId = null,
            byte[]? newHash = null,
            IReadOnlyList<SealedArtifactDeviceRef>? devices = null)
            => new()
            {
                Actor = "tester",
                IdempotencyKey = idempotencyKey ?? Guid.NewGuid(),
                NodeId = nodeId ?? Node.Id.Value,
                AnalysisRunId = analysisRunId ?? Run.Id.Value,
                Devices = devices ??
                [
                    new SealedArtifactDeviceRef
                    {
                        DeviceId = deviceId ?? Device.Id.Value,
                        NewArtifactResourceHash = newHash ?? NewArtifact.ResourceHash.Bytes.ToArray(),
                    },
                ],
            };
    }

    private static List<CanonicalSection> CaptureSections(bool includeRouterOsVersion, bool includePacketPathPairs)
    {
        List<CanonicalSection> sections = [];
        if (includeRouterOsVersion)
        {
            sections.Add(new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.SystemResource,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["version"] = "7.16.2 (stable)",
                        ["board-name"] = "CHR",
                    }),
                ]));
        }

        if (includePacketPathPairs)
        {
            CanonicalRecord pair = new(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ingress"] = "ether1",
                ["egress"] = "wan1",
                ["class"] = "CPU_FIREWALL_PATH",
            });
            sections.Add(new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.TopologyValidation,
                ordered: false,
                [pair]));
            sections.Add(new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.TopologyContainerVeth,
                ordered: false,
                [pair]));
        }

        return sections;
    }

    private static PolicyAnalysisRun CreateAnalysisRun()
        => PolicyAnalysisRun.Create(
            PolicyRevisionId.New(),
            DeploymentTestFactory.H("content"),
            DeploymentTestFactory.H("logical"),
            DeploymentTestFactory.H("analysis-ctx"),
            DeploymentTestFactory.H("evidence-ctx"),
            DeploymentTestFactory.H("topology"),
            DeploymentTestFactory.H("impact"),
            [DeploymentTestFactory.H("device-a")],
            PolicyApprovalHasher.HashDependencyFingerprint(new PolicyApprovalDependencyVector
            {
                CompanyBindingHash = DeploymentTestFactory.H("company"),
                SiteBindingHash = DeploymentTestFactory.H("site"),
                NodeBindingHash = DeploymentTestFactory.H("node"),
                ActiveExceptionsHash = DeploymentTestFactory.H("exc"),
                ZoneBindingHash = DeploymentTestFactory.H("zone"),
                NodeMembershipHash = DeploymentTestFactory.H("members"),
                RouterOsConfigurationHash = DeploymentTestFactory.H("ros"),
                CapabilityHash = DeploymentTestFactory.H("cap"),
                CompatibilityHash = DeploymentTestFactory.H("compat"),
                ManagementAccessProfileHash = DeploymentTestFactory.H("mgmt"),
                AnchorGuardContextHash = DeploymentTestFactory.H("anchor"),
                AnalyzerVersion = PolicyApprovalCodes.AnalyzerVersion,
                PolicySchemaVersion = PolicyDocument.SchemaName,
                PipelineVersion = PolicyPipelineV1.Version,
            }),
            PolicyEvidenceAnalysisCodes.RiskLow,
            evidenceSignalsPresent: true,
            PolicyApprovalCodes.AnalyzerVersion,
            PolicyDocument.SchemaName,
            PolicyPipelineV1.Version,
            [],
            [
                new PolicyApprovalTestOutcome
                {
                    TestId = PolicyTestId.New(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
            UserId.New(),
            DateTimeOffset.UtcNow);

    private static RouterOsFilterArtifact CreateArtifact(
        DeviceId device,
        Hash256? semanticsHash = null,
        FilterBuiltInContext chain = FilterBuiltInContext.Forward)
    {
        Hash256 semantics = semanticsHash ?? SemanticsHash;
        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(ProfileHash, semantics, device);
        string chainCode = chain switch
        {
            FilterBuiltInContext.Input => "i",
            FilterBuiltInContext.Forward => "f",
            FilterBuiltInContext.Output => "o",
            _ => "f",
        };
        string marker = chain switch
        {
            FilterBuiltInContext.Input => "mfc:anchor:v1:4:i",
            FilterBuiltInContext.Forward => "mfc:anchor:v1:4:f",
            FilterBuiltInContext.Output => "mfc:anchor:v1:4:o",
            _ => "mfc:anchor:v1:4:f",
        };
        return RouterOsFilterArtifact.Create(
            ProfileHash,
            semantics,
            device,
            addressLists:
            [
                new AddressListArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    Name = "mfc4.a.deadbeefdeadbeef",
                    Entries = [AddressListEntryArtifact.Create("10.10.0.0/16")],
                },
            ],
            chains:
            [
                new ChainArtifactDraft
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = chain,
                    Name = $"mfc4.{chainCode}.r." + artifactId,
                    Role = FilterChainArtifactRole.Root,
                    Rules =
                    [
                        FilterRuleArtifact.Create(
                            0,
                            "accept",
                            "mfc:rule:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:0",
                            logicalRuleId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                            matchers: new Dictionary<string, string> { ["connection-state"] = "established,related" }),
                    ],
                },
            ],
            anchorTargets:
            [
                AnchorTargetArtifact.Create(
                    IpAddressFamily.IPv4,
                    chain,
                    marker,
                    $"mfc4.{chainCode}.r." + artifactId),
            ]);
    }

    private static CompilationProvenance Provenance(DeviceId device)
        => new()
        {
            DeviceId = device,
            LogicalEffectivePolicyHash = Hash256.ParseHex(
                "3333333333333333333333333333333333333333333333333333333333333333"),
            DeviceResolvedPolicyHash = Hash256.ParseHex(
                "4444444444444444444444444444444444444444444444444444444444444444"),
            AnalysisBundleHash = Hash256.ParseHex(
                "5555555555555555555555555555555555555555555555555555555555555555"),
            CapabilityHash = Hash256.ParseHex(
                "6666666666666666666666666666666666666666666666666666666666666666"),
            CompilerProfileHash = ProfileHash,
            CompilerVersion = "test",
            CompiledAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static StoredFilterArtifact ToMeta(RouterOsFilterArtifact artifact)
        => new()
        {
            ResourceHash = artifact.ResourceHash,
            ArtifactId = artifact.ArtifactId,
            DeviceId = artifact.DeviceId,
            PhysicalSemanticsHash = artifact.PhysicalSemanticsHash,
            CompilerProfileHash = artifact.CompilerProfileHash,
            LogicalEffectivePolicyHash = Hash256.ParseHex(
                "3333333333333333333333333333333333333333333333333333333333333333"),
            DeviceResolvedPolicyHash = Hash256.ParseHex(
                "4444444444444444444444444444444444444444444444444444444444444444"),
            AnalysisBundleHash = Hash256.ParseHex(
                "5555555555555555555555555555555555555555555555555555555555555555"),
            CapabilityHash = Hash256.ParseHex(
                "6666666666666666666666666666666666666666666666666666666666666666"),
            CompilerVersion = "test",
            CompiledAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UncompressedSize = artifact.CanonicalBytes.Length,
            Inserted = true,
        };
}
