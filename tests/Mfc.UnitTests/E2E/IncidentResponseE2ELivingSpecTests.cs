using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Xunit;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOperationState = Mfc.Domain.Deployment.DeploymentOperationState;
using DomainPolicy = Mfc.Domain.Policy.Policy;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;

namespace Mfc.UnitTests.E2E;

/// <summary>Living Spec matrix for Issue Set M7.4-06 AC (incident response E2E scripted).</summary>
public sealed class IncidentResponseE2ELivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid IncidentGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly IncidentId IncidentId = new(IncidentGuid);
    private static readonly byte[] CapabilityHashBytes = DeploymentTestFactory.H("cap").Bytes.ToArray();

    [Fact]
    public async Task Ac1EnforceableAssessReturnsFullyEnforceable()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        ApplicationResult<ResponseIntentFeasibilityView> result = await harness.AssessCpuFirewallAsync();
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ResponseAssessmentFeasibility.FullyEnforceable, result.Value!.Feasibility);
    }

    [Fact]
    public async Task Ac2EnforceableDeployEmitsPlannedAndStartedFeedback()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        await harness.AssessCpuFirewallAsync();
        ApplicationResult<DeployIncidentDenyOverlayView> deploy = await harness.DeployAsync();
        Assert.True(deploy.IsSuccess, deploy.Error?.Message);

        IReadOnlyList<ResponseFeedbackEvent> events = await harness.FeedbackStore
            .ListByIncidentAsync(IncidentId);
        Assert.Contains(events, e => e.EventCode == ResponseFeedbackEventCodes.Planned);
        Assert.Contains(events, e => e.EventCode == ResponseFeedbackEventCodes.Started);
    }

    [Fact]
    public async Task Ac3CommittedDeploymentEmitsAppliedAndVerifiedFeedback()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        ApplicationResult<DeployIncidentDenyOverlayView> deploy = await harness.DeployAsync();
        Assert.True(deploy.IsSuccess, deploy.Error?.Message);

        StandaloneDeploymentResult committed = await harness.ExecuteCommittedDeploymentAsync(deploy.Value!);
        Assert.True(committed.Succeeded, committed.ErrorCode);

        ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>> reported = await harness.ReportOutcome.ExecuteAsync(
            new ReportIncidentDeploymentOutcomeCommand
            {
                Actor = "tester",
                IncidentId = IncidentGuid,
                NodeId = harness.NodeId,
                CorrelationId = deploy.Value!.OperationId,
                DeviceIds = [harness.DeviceId],
                Result = committed,
                PlanHash = deploy.Value.PlanHash,
            });
        Assert.True(reported.IsSuccess, reported.Error?.Message);
        Assert.Contains(reported.Value!, v => v.EventCode == ResponseFeedbackEventCodes.Applied);
        Assert.Contains(reported.Value!, v => v.EventCode == ResponseFeedbackEventCodes.Verified);
    }

    [Fact]
    public async Task Ac4NotEnforceableAssessEmitsBlockedWithoutDeploy()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        ApplicationResult<ResponseIntentFeasibilityView> assess = await harness.AssessL2BypassAsync();
        Assert.True(assess.IsSuccess, assess.Error?.Message);
        Assert.Equal(ResponseAssessmentFeasibility.NotEnforceableByIpFilter, assess.Value!.Feasibility);

        IReadOnlyList<ResponseFeedbackEvent> events = await harness.FeedbackStore
            .ListByIncidentAsync(IncidentId);
        Assert.Contains(events, e => e.EventCode == ResponseFeedbackEventCodes.Blocked);
        Assert.DoesNotContain(events, e => e.EventCode == ResponseFeedbackEventCodes.Started);
    }

    [Fact]
    public async Task Ac5FailedDeploymentRollbackEmitsRolledBackFeedback()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        StandaloneDeploymentResult rolledBack = await harness.ExecuteRolledBackDeploymentAsync();
        Assert.Equal(DomainOperationState.RolledBack, rolledBack.State);

        ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>> reported = await harness.ReportOutcome.ExecuteAsync(
            new ReportIncidentDeploymentOutcomeCommand
            {
                Actor = "tester",
                IncidentId = IncidentGuid,
                NodeId = harness.NodeId,
                CorrelationId = Guid.NewGuid(),
                DeviceIds = [harness.DeviceId],
                Result = rolledBack,
            });
        Assert.True(reported.IsSuccess, reported.Error?.Message);
        Assert.Contains(reported.Value!, v => v.EventCode == ResponseFeedbackEventCodes.RolledBack);
    }

    [Fact]
    public async Task Ac6RecoveryRequiredEmitsRecoveryFeedback()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        StandaloneDeploymentResult failed = new()
        {
            Succeeded = false,
            State = DomainOperationState.Failed,
            ErrorCode = DeploymentCodes.RecoveryRequired,
            Timeline = ["verify:failed"],
            WroteToDevice = true,
            WatchdogArmedBeforeActivation = true,
            WatchdogDisarmedBeforeCommit = false,
            DetachedArtifactPreservedOnFailure = true,
        };

        ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>> reported = await harness.ReportOutcome.ExecuteAsync(
            new ReportIncidentDeploymentOutcomeCommand
            {
                Actor = "tester",
                IncidentId = IncidentGuid,
                NodeId = harness.NodeId,
                CorrelationId = Guid.NewGuid(),
                DeviceIds = [harness.DeviceId],
                Result = failed,
            });
        Assert.True(reported.IsSuccess, reported.Error?.Message);
        Assert.Contains(reported.Value!, v => v.EventCode == ResponseFeedbackEventCodes.RecoveryRequired);
    }

    [Fact]
    public async Task Ac7PartialEnforceabilityRecordsResidualRisk()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        ApplicationResult<ResponseIntentFeasibilityView> assess = await harness.AssessFastTrackAsync();
        Assert.True(assess.IsSuccess, assess.Error?.Message);
        Assert.Equal(ResponseAssessmentFeasibility.NewConnectionsOnly, assess.Value!.Feasibility);

        IReadOnlyList<ResponseFeedbackEvent> events = await harness.FeedbackStore
            .ListByIncidentAsync(IncidentId);
        ResponseFeedbackEvent planned = Assert.Single(events, e => e.EventCode == ResponseFeedbackEventCodes.Planned);
        Assert.Equal(ResponseAssessmentFeasibility.NewConnectionsOnly.ToString(), planned.ResidualRisk);
    }

    [Fact]
    public async Task Ac8TtlExpiryEmitsExpiredAndRemovalPlannedFeedback()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync(expiredBinding: true);
        ApplicationResult<PlanIncidentDenyOverlayRemovalView> removal = await harness.PlanRemovalAsync();
        Assert.True(removal.IsSuccess, removal.Error?.Message);

        IReadOnlyList<ResponseFeedbackEvent> events = await harness.FeedbackStore
            .ListByIncidentAsync(IncidentId);
        Assert.Contains(events, e => e.EventCode == ResponseFeedbackEventCodes.Expired);
        Assert.Contains(events, e => e.EventCode == ResponseFeedbackEventCodes.Planned);
    }

    [Fact]
    public async Task Ac9FullEnforceableLifecycleQueryableByIncident()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        await harness.AssessCpuFirewallAsync();
        ApplicationResult<DeployIncidentDenyOverlayView> deploy = await harness.DeployAsync();
        Assert.True(deploy.IsSuccess, deploy.Error?.Message);
        StandaloneDeploymentResult committed = await harness.ExecuteCommittedDeploymentAsync(deploy.Value!);
        await harness.ReportOutcome.ExecuteAsync(new ReportIncidentDeploymentOutcomeCommand
        {
            Actor = "tester",
            IncidentId = IncidentGuid,
            NodeId = harness.NodeId,
            CorrelationId = deploy.Value!.OperationId,
            DeviceIds = [harness.DeviceId],
            Result = committed,
            PlanHash = deploy.Value.PlanHash,
        });

        ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>> listed = await harness.ListFeedback.ExecuteAsync(
            new ListResponseFeedbackEventsCommand
            {
                Actor = "tester",
                IncidentId = IncidentGuid,
            });
        Assert.True(listed.IsSuccess, listed.Error?.Message);
        string[] codes = listed.Value!.Select(static v => v.EventCode).ToArray();
        Assert.Contains(ResponseFeedbackEventCodes.Planned, codes);
        Assert.Contains(ResponseFeedbackEventCodes.Started, codes);
        Assert.Contains(ResponseFeedbackEventCodes.Applied, codes);
        Assert.Contains(ResponseFeedbackEventCodes.Verified, codes);
    }

    [Fact]
    public async Task Ac10UnauthorizedAssessRejected()
    {
        E2EHarness harness = await E2EHarness.CreateReadyAsync();
        harness.Auth.DeniedPermissions.Add(ApplicationPermissions.IncidentResponseAssess);
        ApplicationResult<ResponseIntentFeasibilityView> result = await harness.AssessCpuFirewallAsync();
        Assert.False(result.IsSuccess);
    }

    private sealed class E2EHarness
    {
        public FakeAuthorizationBoundary Auth { get; }

        public FakeResponseFeedbackEventStore FeedbackStore { get; }

        public AssessResponseIntentFeasibilityUseCase Assess { get; }

        public DeployIncidentDenyOverlayUseCase Deploy { get; }

        public ReportIncidentDeploymentOutcomeUseCase ReportOutcome { get; }

        public ListResponseFeedbackEventsUseCase ListFeedback { get; }

        public PlanIncidentDenyOverlayRemovalUseCase PlanRemoval { get; }

        public DomainNode Node { get; }

        public Guid NodeId { get; }

        public Guid DeviceId { get; }

        public Guid OverlayPolicyId { get; }

        public Guid BindingId { get; }

        public ulong BindingRowVersion { get; }

        public CompileNodeFilterArtifactsUseCaseTests.CompileFixture CompileFixture { get; }

        private readonly Guid _analysisRunId;

        private readonly byte[] _fingerprint;

        private E2EHarness(
            FakeAuthorizationBoundary auth,
            FakeResponseFeedbackEventStore feedbackStore,
            AssessResponseIntentFeasibilityUseCase assess,
            DeployIncidentDenyOverlayUseCase deploy,
            ReportIncidentDeploymentOutcomeUseCase reportOutcome,
            ListResponseFeedbackEventsUseCase listFeedback,
            PlanIncidentDenyOverlayRemovalUseCase planRemoval,
            DomainNode node,
            Guid overlayPolicyId,
            Guid bindingId,
            ulong bindingRowVersion,
            CompileNodeFilterArtifactsUseCaseTests.CompileFixture compileFixture)
        {
            Auth = auth;
            FeedbackStore = feedbackStore;
            Assess = assess;
            Deploy = deploy;
            ReportOutcome = reportOutcome;
            ListFeedback = listFeedback;
            PlanRemoval = planRemoval;
            Node = node;
            NodeId = compileFixture.NodeId;
            DeviceId = node.Devices[0].Id.Value;
            OverlayPolicyId = overlayPolicyId;
            BindingId = bindingId;
            BindingRowVersion = bindingRowVersion;
            CompileFixture = compileFixture;
            _analysisRunId = compileFixture.RunId;
            _fingerprint = compileFixture.Fingerprint;
        }

        public static async Task<E2EHarness> CreateReadyAsync(bool expiredBinding = false)
        {
            CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx =
                await SeedOverlayFixtureAsync(bindOverlay: true);
            FakeAuthorizationBoundary auth = new();
            FakeResponseFeedbackEventStore feedbackStore = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new() { UtcNow = T0 };
            FakeDeploymentStore deployments = new();
            FakeDriftEventStore drift = new();
            FakeIdempotencyStore idempotency = new();
            EmitResponseFeedbackUseCase feedback = ResponseFeedbackTestFactory.CreateEmit(auth, feedbackStore, audit, clock);
            AssessResponseIntentFeasibilityUseCase assess = new(auth, feedback);
            CreateDeploymentPlanUseCase createPlan = new(
                auth, fx.Nodes, deployments, idempotency, audit, clock,
                new Mfc.Application.Topology.VrrpPairConsistencyLoader(
                    new FakeDeviceStore(), new FakeSnapshotStore(), new FakeDeviceHashStateStore()));
            E2EScriptedDeploymentRuntime runtime = new() { Commit = true };
            StartDeploymentUseCase start = new(auth, fx.Nodes, deployments, drift, idempotency, audit, clock, runtime);
            DeployIncidentDenyOverlayUseCase deploy = new(
                auth, fx.Policies, fx.Approvals, audit, fx.UseCase, createPlan, start, feedback);
            ReportIncidentDeploymentOutcomeUseCase reportOutcome = new(feedback);
            ListResponseFeedbackEventsUseCase listFeedback = new(auth, feedbackStore);
            ExpireIncidentDenyOverlayBindingUseCase expire = new(
                auth, fx.Approvals, idempotency, audit, clock, fx.Policies, feedback);
            PlanIncidentDenyOverlayRemovalUseCase planRemoval = new(
                auth, fx.Policies, fx.Approvals, audit, expire, fx.UseCase, createPlan, feedback);

            DomainPolicy overlay = (await fx.Policies.ListActiveByOwnerAsync(
                PolicyKind.IncidentDenyOverlay,
                fx.NodeId)).Single();
            PolicyDesiredBinding binding = (await fx.Approvals.ListActiveBindingsAsync(
                PolicyBindingScope.IncidentDenyOverlay,
                fx.NodeId)).Single(b => b.PolicyId == overlay.Id);
            if (expiredBinding)
            {
                binding = PolicyDesiredBinding.Reconstitute(
                    binding.Id,
                    binding.Scope,
                    binding.ScopeId,
                    binding.PolicyId,
                    binding.DesiredRevisionId,
                    binding.AnalysisRunId,
                    binding.BundleHash,
                    binding.State,
                    binding.ValidFromUtc,
                    T0.AddHours(-1),
                    binding.RowVersion,
                    binding.CreatedAtUtc,
                    binding.UpdatedAtUtc);
                await fx.Approvals.SaveBindingAsync(binding);
            }

            DomainNode? node = await fx.Nodes.GetAsync(new NodeId(fx.NodeId));
            Assert.NotNull(node);
            return new E2EHarness(
                auth,
                feedbackStore,
                assess,
                deploy,
                reportOutcome,
                listFeedback,
                planRemoval,
                node,
                overlay.Id.Value,
                binding.Id.Value,
                binding.RowVersion,
                fx);
        }

        public Task<ApplicationResult<ResponseIntentFeasibilityView>> AssessCpuFirewallAsync()
            => Assess.ExecuteAsync(new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = SampleQuery(fastTrack: false, l2Bypass: false),
            });

        public Task<ApplicationResult<ResponseIntentFeasibilityView>> AssessL2BypassAsync()
            => Assess.ExecuteAsync(new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = SampleQuery(fastTrack: false, l2Bypass: true),
            });

        public Task<ApplicationResult<ResponseIntentFeasibilityView>> AssessFastTrackAsync()
            => Assess.ExecuteAsync(new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = SampleQuery(fastTrack: true, l2Bypass: false),
            });

        public Task<ApplicationResult<DeployIncidentDenyOverlayView>> DeployAsync()
            => Deploy.ExecuteAsync(new DeployIncidentDenyOverlayCommand
            {
                Actor = "tester",
                NodeId = NodeId,
                OverlayPolicyId = OverlayPolicyId,
                AnalysisRunId = _analysisRunId,
                CurrentDependencyFingerprint = _fingerprint,
                CurrentCapabilityHash = CapabilityHashBytes,
                PlanIdempotencyKey = Guid.NewGuid(),
                DeployIdempotencyKey = Guid.NewGuid(),
                LogicalPolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
                AnalysisBundleHash = DeploymentTestFactory.H("analysis").Bytes.ToArray(),
                TopologyProjectionHash = DeploymentTestFactory.H("topology").Bytes.ToArray(),
                DevicePlans = [DeploymentTestFactory.DevicePlan(new DeviceId(DeviceId), Node.DeclaredKind)],
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            });

        public Task<ApplicationResult<PlanIncidentDenyOverlayRemovalView>> PlanRemovalAsync()
            => PlanRemoval.ExecuteAsync(new PlanIncidentDenyOverlayRemovalCommand
            {
                Actor = "tester",
                NodeId = NodeId,
                OverlayPolicyId = OverlayPolicyId,
                BindingId = BindingId,
                ExpectedBindingRowVersion = BindingRowVersion,
                ExpireIdempotencyKey = Guid.NewGuid(),
                PlanIdempotencyKey = Guid.NewGuid(),
                AnalysisRunId = _analysisRunId,
                CurrentDependencyFingerprint = _fingerprint,
                CurrentCapabilityHash = CapabilityHashBytes,
                LogicalPolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
                AnalysisBundleHash = DeploymentTestFactory.H("analysis").Bytes.ToArray(),
                TopologyProjectionHash = DeploymentTestFactory.H("topology").Bytes.ToArray(),
                DevicePlans = [DeploymentTestFactory.DevicePlan(new DeviceId(DeviceId), Node.DeclaredKind)],
            });

        public async Task<StandaloneDeploymentResult> ExecuteCommittedDeploymentAsync(DeployIncidentDenyOverlayView deploy)
        {
            DeploymentPlan plan = DeploymentTestFactory.PlanFor(Node, T0);
            DeploymentOperation operation = DeploymentOperation.Create(plan, Node, UserId.New(), T0);
            DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
            RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);
            return await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
                Node,
                plan,
                operation,
                deviceState,
                new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
                [],
                DeploymentTestFactory.CpuPairs(),
                [],
                [],
                plan.DevicePlans[0].NewArtifactHash,
                T0.AddMinutes(1),
                T0);
        }

        public async Task<StandaloneDeploymentResult> ExecuteRolledBackDeploymentAsync()
        {
            DeploymentPlan plan = DeploymentTestFactory.PlanFor(Node, T0, includeIpv6: true);
            DeploymentOperation operation = DeploymentOperation.Create(plan, Node, UserId.New(), T0);
            DeviceDeployment deviceState = DeviceDeployment.Create(operation.Id, plan.DevicePlans[0].DeviceId, T0);
            RecordingChannel channel = DeploymentAcceptanceHarness.SeedChannel(plan, toNew: false);
            channel.FailIpv6FilterSets = true;
            return await ExecuteStandaloneDeploymentUseCase.ExecuteAsync(
                Node,
                plan,
                operation,
                deviceState,
                new FakeRuntime(plan.DevicePlans[0].DeviceId, channel),
                [],
                DeploymentTestFactory.CpuPairs(),
                [],
                [],
                plan.DevicePlans[0].NewArtifactHash,
                T0.AddMinutes(1),
                T0);
        }

        private ResponseIntentFeasibilityQuery SampleQuery(bool fastTrack, bool l2Bypass)
            => new()
            {
                Intent = ResponseIntent.Create(
                    IncidentId,
                    new NodeId(NodeId),
                    ResponseIntentAction.TemporaryPreStateDeny,
                    TrafficPredicate.Create(),
                    T0.AddHours(2),
                    ResponseIntentUrgency.Normal,
                    ["evt:e2e"],
                    "analyst",
                    Guid.NewGuid()),
                PacketPathClass = ObservedPacketPathClass.CpuFirewall,
                SessionVisibility = SessionVisibilityStatus.Full,
                FastTrackSessionActive = fastTrack,
                L2BridgeVlanBypass = l2Bypass,
            };
    }

    private static async Task<CompileNodeFilterArtifactsUseCaseTests.CompileFixture> SeedOverlayFixtureAsync(bool bindOverlay)
    {
        CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx =
            await CompileNodeFilterArtifactsUseCaseTests.SeedApprovedCompanyWithNodeDeviceAsync(
                withCapabilitySnapshot: true,
                withChainContracts: true);
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
                "e2e",
                ["evt:e2e"]));
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

        return fx;
    }

    private sealed class E2EScriptedDeploymentRuntime : IDeploymentRuntime
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
