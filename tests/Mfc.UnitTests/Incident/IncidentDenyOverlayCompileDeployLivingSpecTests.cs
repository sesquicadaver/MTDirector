using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Xunit;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainPolicy = Mfc.Domain.Policy.Policy;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.4-03 AC (incident overlay compile/deploy via M3/M4).</summary>
public sealed class IncidentDenyOverlayCompileDeployLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 23, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid NodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly IncidentId IncidentId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly byte[] CapabilityHashBytes = DeploymentTestFactory.H("cap").Bytes.ToArray();

    [Fact]
    public void Ac1MergeWithoutOverlaysPreservesComposedRules()
    {
        PolicyRule baseline = SampleRule(PolicyPipelineStage.ProtectedControlPlane, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        IncidentDenyOverlayCompileMerge.MergeResult result =
            IncidentDenyOverlayCompileMerge.Merge([baseline], [], T0);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Rules);
        Assert.Equal(baseline.Id, result.Rules[0].Id);
        Assert.Equal(0, result.ActiveOverlayCount);
    }

    [Fact]
    public void Ac2ExpiredOverlayIsSkippedAtMerge()
    {
        PolicyRule baseline = SampleRule(PolicyPipelineStage.ProtectedControlPlane, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        PolicyLayer expired = OverlayLayer(
            SampleRule(PolicyPipelineStage.IncidentPreStateDeny, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            T0.AddHours(-2));
        IncidentDenyOverlayCompileMerge.MergeResult result =
            IncidentDenyOverlayCompileMerge.Merge([baseline], [expired], T0);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Rules);
        Assert.Equal(0, result.ActiveOverlayCount);
    }

    [Fact]
    public void Ac3RuleUuidCollisionFailsClosed()
    {
        Guid shared = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        PolicyRule baseline = SampleRule(PolicyPipelineStage.ProtectedControlPlane, shared);
        PolicyLayer overlay = OverlayLayer(
            SampleRule(PolicyPipelineStage.IncidentPreStateDeny, shared),
            T0.AddHours(1));
        IncidentDenyOverlayCompileMerge.MergeResult result =
            IncidentDenyOverlayCompileMerge.Merge([baseline], [overlay], T0);
        Assert.False(result.IsSuccess);
        Assert.Equal(IncidentDenyOverlayCodes.RuleUuidCollision, result.Code);
    }

    [Fact]
    public void Ac4InvalidOverlayDocumentFailsMerge()
    {
        PolicyDocument invalid = new(
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            rules:
            [
                SampleRule(PolicyPipelineStage.NodeDeny, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            ],
            incidentDenyOverlayMetadata: SampleMetadata());
        PolicyLayer overlay = new()
        {
            PolicyId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            Kind = PolicyKind.IncidentDenyOverlay,
            OwnerScope = PolicyOwnerScope.Node,
            OwnerId = NodeId,
            ContentHash = Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray()),
            ParentContextHash = null,
            PolicyDocument = invalid,
        };
        IncidentDenyOverlayCompileMerge.MergeResult result =
            IncidentDenyOverlayCompileMerge.Merge([], [overlay], T0);
        Assert.False(result.IsSuccess);
        Assert.Equal(IncidentDenyOverlayCodes.StageViolation, result.Code);
    }

    [Fact]
    public void Ac5MergeOrdersIncidentRulesByPipelineStage()
    {
        PolicyRule protect = SampleRule(PolicyPipelineStage.ProtectedControlPlane, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        PolicyRule mandatory = SampleRule(PolicyPipelineStage.MandatoryPreStateDeny, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        PolicyRule incident = SampleRule(PolicyPipelineStage.IncidentPreStateDeny, Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        PolicyLayer overlay = OverlayLayer(incident, T0.AddHours(1));
        IncidentDenyOverlayCompileMerge.MergeResult result =
            IncidentDenyOverlayCompileMerge.Merge([protect, mandatory], [overlay], T0);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Rules.Count);
        Assert.Equal(protect.Id, result.Rules[0].Id);
        Assert.Equal(incident.Id, result.Rules[1].Id);
        Assert.Equal(mandatory.Id, result.Rules[2].Id);
    }

    [Fact]
    public async Task Ac6BoundOverlayIncreasesCompiledRuleCount()
    {
        CompileNodeFilterArtifactsUseCaseTests.CompileFixture withOverlay =
            await SeedCompileFixtureAsync(bindOverlay: true);
        ApplicationResult<CompileNodeFilterArtifactsView> compiled = await withOverlay.UseCase.ExecuteAsync(CompileCommand(withOverlay));
        Assert.True(compiled.IsSuccess, compiled.Error?.Message);
        int withRules = compiled.Value!.Artifacts[0].RuleCount;

        CompileNodeFilterArtifactsUseCaseTests.CompileFixture baseline =
            await SeedCompileFixtureAsync(bindOverlay: false);
        ApplicationResult<CompileNodeFilterArtifactsView> without = await baseline.UseCase.ExecuteAsync(CompileCommand(baseline));
        Assert.True(without.IsSuccess, without.Error?.Message);
        Assert.True(withRules > without.Value!.Artifacts[0].RuleCount);
    }

    [Fact]
    public async Task Ac7DeployRejectsOverlayPolicyForWrongNode()
    {
        DeployHarness harness = DeployHarness.CreateEmpty();
        DomainPolicy overlay = DomainPolicy.Create(
            NonEmptyName.Create("overlay"),
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            Guid.NewGuid());
        await harness.Policies.AddPolicyAsync(overlay);
        ApplicationResult<DeployIncidentDenyOverlayView> result = await harness.UseCase.ExecuteAsync(
            SampleDeployCommand(NodeId, overlay.Id.Value));
        Assert.False(result.IsSuccess);
        Assert.Equal(IncidentDenyOverlayCodes.OverlayNodeMismatch, result.Error!.Code);
    }

    [Fact]
    public async Task Ac8DeployRequiresIncidentDenyOverlayKind()
    {
        DeployHarness harness = DeployHarness.CreateEmpty();
        DomainPolicy company = DomainPolicy.Create(
            NonEmptyName.Create("baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            null);
        await harness.Policies.AddPolicyAsync(company);
        ApplicationResult<DeployIncidentDenyOverlayView> result = await harness.UseCase.ExecuteAsync(
            SampleDeployCommand(NodeId, company.Id.Value));
        Assert.False(result.IsSuccess);
        Assert.Equal(IncidentDenyOverlayCodes.WrongKind, result.Error!.Code);
    }

    [Fact]
    public async Task Ac9DeployOrchestratesCompilePlanAndStartForOneNode()
    {
        DeployHarness harness = await DeployHarness.CreateReadyAsync();
        ApplicationResult<DeployIncidentDenyOverlayView> result = await harness.UseCase.ExecuteAsync(
            harness.DeployCommand());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(harness.OverlayPolicyId, result.Value!.OverlayPolicyId);
        Assert.NotEqual(Guid.Empty, result.Value.PlanId);
        Assert.NotEqual(Guid.Empty, result.Value.OperationId);
        Assert.NotEmpty(result.Value.Artifacts);
        Assert.Contains(harness.Audit.Events, e => e.Action == DeployIncidentDenyOverlayUseCase.Operation);
        Assert.Contains(harness.Audit.Events, e => e.Action == CreateDeploymentPlanUseCase.Operation);
        Assert.Contains(harness.Audit.Events, e => e.Action == StartDeploymentUseCase.Operation);
    }

    [Fact]
    public async Task Ac10DeployUseCaseRejectsUnauthorizedActor()
    {
        DeployHarness harness = await DeployHarness.CreateReadyAsync();
        harness.Auth.DeniedPermissions.Add(ApplicationPermissions.IncidentOverlayDeploy);
        ApplicationResult<DeployIncidentDenyOverlayView> result = await harness.UseCase.ExecuteAsync(harness.DeployCommand());
        Assert.False(result.IsSuccess);
    }

    private static CompileNodeFilterArtifactsCommand CompileCommand(
        CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx)
        => new()
        {
            Actor = "tester",
            NodeId = fx.NodeId,
            AnalysisRunId = fx.RunId,
            CurrentDependencyFingerprint = fx.Fingerprint,
            CurrentCapabilityHash = CapabilityHashBytes,
        };

    private static async Task<CompileNodeFilterArtifactsUseCaseTests.CompileFixture> SeedCompileFixtureAsync(bool bindOverlay)
    {
        CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx =
            await CompileNodeFilterArtifactsUseCaseTests.SeedApprovedCompanyWithNodeDeviceAsync(
                withCapabilitySnapshot: true,
                withChainContracts: true);
        await AddOverlayAsync(fx, bindOverlay);
        return fx;
    }

    private static async Task AddOverlayAsync(
        CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx,
        bool bindOverlay)
    {
        DomainPolicy overlayPolicy = DomainPolicy.Create(
            NonEmptyName.Create("incident-overlay"),
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            fx.NodeId);
        await fx.Policies.AddPolicyAsync(overlayPolicy);
        PolicyDocument document = new(
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            rules:
            [
                PolicyRule.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    PolicyPipelineStage.IncidentPreStateDeny,
                    ordinal: 0,
                    TrafficPredicate.Create(),
                    RuleEffectSpec.Create(PolicyRuleEffect.Drop)),
            ],
            incidentDenyOverlayMetadata: IncidentDenyOverlayMetadata.Create(
                IncidentId,
                fx.NodeId,
                T0.AddHours(2),
                "callback",
                ["evt:1"]));
        byte[] canonical = PolicyCanonicalWriter.Write(document);
        PolicyRevision revision = PolicyRevision.Reconstitute(
            PolicyRevisionId.New(),
            overlayPolicy.Id,
            revisionNumber: 1,
            schemaVersion: 1,
            contentHash: PolicyHashing.HashContent(canonical),
            parentContextHash: null,
            state: PolicyRevisionState.Approved,
            createdBy: UserId.New(),
            createdAtUtc: T0,
            approvedAtUtc: T0,
            canonicalBytes: canonical,
            approvedAnalysisRunId: new PolicyAnalysisRunId(fx.RunId),
            approvedBundleHash: Hash256.Create(fx.BundleHash));
        await fx.Policies.AddRevisionAsync(revision);
        if (bindOverlay)
        {
            await fx.Approvals.AddBindingAsync(PolicyDesiredBinding.Reconstitute(
                PolicyBindingId.New(),
                PolicyBindingScope.IncidentDenyOverlay,
                fx.NodeId,
                overlayPolicy.Id,
                revision.Id,
                new PolicyAnalysisRunId(fx.RunId),
                Hash256.Create(fx.BundleHash),
                PolicyBindingState.Active,
                validFromUtc: T0,
                validUntilUtc: T0.AddHours(2),
                rowVersion: 1,
                T0,
                T0));
        }
    }

    private static DeployIncidentDenyOverlayCommand SampleDeployCommand(Guid nodeId, Guid overlayPolicyId)
        => new()
        {
            Actor = "tester",
            NodeId = nodeId,
            OverlayPolicyId = overlayPolicyId,
            AnalysisRunId = Guid.NewGuid(),
            CurrentDependencyFingerprint = Enumerable.Repeat((byte)9, 32).ToArray(),
            CurrentCapabilityHash = CapabilityHashBytes,
            PlanIdempotencyKey = Guid.NewGuid(),
            DeployIdempotencyKey = Guid.NewGuid(),
            LogicalPolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
            AnalysisBundleHash = DeploymentTestFactory.H("analysis").Bytes.ToArray(),
            TopologyProjectionHash = DeploymentTestFactory.H("topology").Bytes.ToArray(),
            DevicePlans = [],
            PacketPathPairs = DeploymentTestFactory.CpuPairs(),
        };

    private static PolicyRule SampleRule(PolicyPipelineStage stage, Guid id)
    {
        PolicyRuleEffect effect = stage switch
        {
            PolicyPipelineStage.ProtectedControlPlane => PolicyRuleEffect.Accept,
            PolicyPipelineStage.IncidentPreStateDeny => PolicyRuleEffect.Drop,
            _ => PolicyRuleEffect.Drop,
        };
        return PolicyRule.Reconstitute(
            new RuleId(id),
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            stage,
            ordinal: 0,
            enabled: true,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(effect),
            LogSpecification.Disabled,
            exceptionEligible: false,
            description: null);
    }

    private static IncidentDenyOverlayMetadata SampleMetadata(DateTimeOffset? expiry = null)
        => IncidentDenyOverlayMetadata.Create(
            IncidentId,
            NodeId,
            expiry ?? T0.AddHours(1),
            "malware callback",
            ["evt:abc123"]);

    private static PolicyLayer OverlayLayer(PolicyRule rule, DateTimeOffset expiry)
        => new()
        {
            PolicyId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            Kind = PolicyKind.IncidentDenyOverlay,
            OwnerScope = PolicyOwnerScope.Node,
            OwnerId = NodeId,
            ContentHash = Hash256.Create(Enumerable.Repeat((byte)2, 32).ToArray()),
            ParentContextHash = null,
            PolicyDocument = new PolicyDocument(
                PolicyKind.IncidentDenyOverlay,
                PolicyOwnerScope.Node,
                rules: [rule],
                incidentDenyOverlayMetadata: SampleMetadata(expiry)),
        };

    private sealed class DeployHarness
    {
        public FakeAuthorizationBoundary Auth { get; }

        public FakeAuditEventWriter Audit { get; }

        public FakePolicyStore Policies { get; }

        public DeployIncidentDenyOverlayUseCase UseCase { get; }

        public Guid NodeId { get; set; }

        public Guid OverlayPolicyId { get; set; }

        public CompileNodeFilterArtifactsUseCaseTests.CompileFixture CompileFixture { get; set; } = null!;

        public DomainNode Node { get; set; } = null!;

        private DeployHarness(
            FakeAuthorizationBoundary auth,
            FakeAuditEventWriter audit,
            FakePolicyStore policies,
            DeployIncidentDenyOverlayUseCase useCase)
        {
            Auth = auth;
            Audit = audit;
            Policies = policies;
            UseCase = useCase;
        }

        public static DeployHarness CreateEmpty()
        {
            FakeAuthorizationBoundary auth = new();
            FakePolicyApprovalStore approvals = new();
            FakePolicyStore policies = new();
            FakeNodeStore nodes = new();
            FakeDeviceStore devices = new();
            FakeZoneDefinitionStore zones = new();
            FakeNodeZoneBindingStore bindings = new();
            FakeZoneResolveObservationSource observations = new();
            FakeSnapshotStore snapshots = new();
            FakeFilterArtifactStore artifacts = new();
            FakeDeploymentStore deployments = new();
            FakeDriftEventStore drift = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new() { UtcNow = T0 };
            ScriptedDeploymentRuntime runtime = new() { Commit = true };
            DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
            nodes.AddAsync(node).GetAwaiter().GetResult();
            FakeResponseFeedbackEventStore feedbackStore = new();
            EmitResponseFeedbackUseCase feedback = ResponseFeedbackTestFactory.CreateEmit(auth, feedbackStore, audit, clock);
            CompileNodeFilterArtifactsUseCase compile = new(
                auth, nodes, devices, policies, approvals, zones, bindings, observations, snapshots, artifacts, clock);
            CreateDeploymentPlanUseCase createPlan = new(auth, nodes, deployments, idempotency, audit, clock);
            StartDeploymentUseCase start = new(auth, nodes, deployments, drift, idempotency, audit, clock, runtime);
            return new DeployHarness(
                auth,
                audit,
                policies,
                new DeployIncidentDenyOverlayUseCase(auth, policies, approvals, audit, compile, createPlan, start, feedback));
        }

        public static async Task<DeployHarness> CreateReadyAsync()
        {
            CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx =
                await SeedCompileFixtureAsync(bindOverlay: true);
            FakeAuthorizationBoundary auth = new();
            FakeDeploymentStore deployments = new();
            FakeDriftEventStore drift = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new() { UtcNow = T0 };
            ScriptedDeploymentRuntime runtime = new() { Commit = true };
            FakeResponseFeedbackEventStore feedbackStore = new();
            EmitResponseFeedbackUseCase feedback = ResponseFeedbackTestFactory.CreateEmit(auth, feedbackStore, audit, clock);
            CreateDeploymentPlanUseCase createPlan = new(auth, fx.Nodes, deployments, idempotency, audit, clock);
            StartDeploymentUseCase start = new(auth, fx.Nodes, deployments, drift, idempotency, audit, clock, runtime);
            DeployIncidentDenyOverlayUseCase deploy = new(
                auth, fx.Policies, fx.Approvals, audit, fx.UseCase, createPlan, start, feedback);
            DomainPolicy overlay = (await fx.Policies.ListActiveByOwnerAsync(
                PolicyKind.IncidentDenyOverlay,
                fx.NodeId)).Single();
            DomainNode? node = await fx.Nodes.GetAsync(new NodeId(fx.NodeId));
            Assert.NotNull(node);
            return new DeployHarness(auth, audit, fx.Policies, deploy)
            {
                CompileFixture = fx,
                NodeId = fx.NodeId,
                OverlayPolicyId = overlay.Id.Value,
                Node = node,
            };
        }

        public DeployIncidentDenyOverlayCommand DeployCommand()
        {
            Device device = Node.Devices[0];
            return new DeployIncidentDenyOverlayCommand
            {
                Actor = "tester",
                NodeId = CompileFixture.NodeId,
                OverlayPolicyId = OverlayPolicyId,
                AnalysisRunId = CompileFixture.RunId,
                CurrentDependencyFingerprint = CompileFixture.Fingerprint,
                CurrentCapabilityHash = CapabilityHashBytes,
                PlanIdempotencyKey = Guid.NewGuid(),
                DeployIdempotencyKey = Guid.NewGuid(),
                LogicalPolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
                AnalysisBundleHash = DeploymentTestFactory.H("analysis").Bytes.ToArray(),
                TopologyProjectionHash = DeploymentTestFactory.H("topology").Bytes.ToArray(),
                DevicePlans = [DeploymentTestFactory.DevicePlan(device.Id, Node.DeclaredKind)],
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            };
        }
    }

    private sealed class ScriptedDeploymentRuntime : IDeploymentRuntime
    {
        public bool Commit { get; init; } = true;

        public Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
            DomainNode node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            IReadOnlyList<PacketPathPairFact> packetPathPairs,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            DomainState terminal = Commit ? DomainState.Committed : DomainState.Failed;
            return Task.FromResult(new DeploymentWorkflowExecutionResult
            {
                Succeeded = Commit,
                State = terminal,
                Timeline = ["execute"],
                ActivationStarted = true,
            });
        }

        public Task<DeploymentWorkflowRollbackResult> RollbackAsync(
            DomainNode node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWorkflowRollbackResult
            {
                Succeeded = true,
                State = DomainState.RolledBack,
                Timeline = ["rollback"],
            });

        public Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
            DomainNode node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeploymentWorkflowRecoveryResult
            {
                Action = DeploymentRecoveryAction.MarkFailedOrCanceled,
                State = DomainState.Failed,
                Timeline = ["recover"],
            });
    }
}
