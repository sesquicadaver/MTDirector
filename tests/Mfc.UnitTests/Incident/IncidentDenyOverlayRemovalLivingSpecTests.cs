using System.Reflection;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Incident;
using Mfc.Application.Jobs;
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

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.4-04 AC (incident overlay TTL removal plan).</summary>
public sealed class IncidentDenyOverlayRemovalLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiredAt = T0.AddHours(-1);
    private static readonly Guid NodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly IncidentId IncidentId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly byte[] CapabilityHashBytes = DeploymentTestFactory.H("cap").Bytes.ToArray();

    [Fact]
    public void Ac1ExpirePendingRemovalRequiresIncidentOverlayScope()
    {
        PolicyDesiredBinding exception = SampleBinding(PolicyBindingScope.Exception, ExpiredAt);
        Assert.Throws<DomainInvariantException>(() => exception.ExpirePendingRemoval(T0));
    }

    [Fact]
    public void Ac2EvaluateIncidentOverlayExpiryRejectsBeforeValidUntil()
    {
        PolicyDesiredBinding binding = SampleBinding(PolicyBindingScope.IncidentDenyOverlay, T0.AddHours(1));
        PolicyBindingEvaluation evaluation = PolicyBindingGate.EvaluateIncidentOverlayExpiry(binding, T0);
        Assert.False(evaluation.Allowed);
        Assert.Equal(PolicyApprovalCodes.BindingNotDue, evaluation.ErrorCode);
    }

    [Fact]
    public void Ac3EvaluateIncidentOverlayExpiryAllowsPastDueBinding()
    {
        PolicyDesiredBinding binding = SampleBinding(PolicyBindingScope.IncidentDenyOverlay, ExpiredAt);
        PolicyBindingEvaluation evaluation = PolicyBindingGate.EvaluateIncidentOverlayExpiry(binding, T0);
        Assert.True(evaluation.Allowed);
    }

    [Fact]
    public async Task Ac4ListDueIncidentDenyOverlayBindingsReturnsPastDueActiveOnly()
    {
        FakePolicyApprovalStore approvals = new();
        PolicyDesiredBinding due = SampleBinding(PolicyBindingScope.IncidentDenyOverlay, ExpiredAt);
        PolicyDesiredBinding future = SampleBinding(PolicyBindingScope.IncidentDenyOverlay, T0.AddHours(2));
        await approvals.AddBindingAsync(due);
        await approvals.AddBindingAsync(future);
        IReadOnlyList<PolicyDesiredBinding> listed = await approvals.ListDueIncidentDenyOverlayBindingsAsync(T0, 10);
        Assert.Single(listed);
        Assert.Equal(due.Id.Value, listed[0].Id.Value);
    }

    [Fact]
    public async Task Ac5ExpireUseCaseHasZeroRouterOsDependencies()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyApprovalStore approvals = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = T0 };
        FakePolicyStore policies = new();
        FakeResponseFeedbackEventStore feedbackStore = new();
        EmitResponseFeedbackUseCase feedback = ResponseFeedbackTestFactory.CreateEmit(auth, feedbackStore, audit, clock);
        ExpireIncidentDenyOverlayBindingUseCase expire = new(auth, approvals, idempotency, audit, clock, policies, feedback);
        PolicyDesiredBinding binding = SampleBinding(PolicyBindingScope.IncidentDenyOverlay, ExpiredAt);
        await approvals.AddBindingAsync(binding);

        ConstructorInfo ctor = typeof(ExpireIncidentDenyOverlayBindingUseCase).GetConstructors().Single();
        Assert.DoesNotContain(
            ctor.GetParameters(),
            static p => p.ParameterType.FullName is not null
                        && (p.ParameterType.FullName.Contains("RouterOs", StringComparison.Ordinal)
                            || p.ParameterType.Name.Contains("DeploymentSession", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Ac6ExpireTransitionsBindingWithoutDeploymentStart()
    {
        RemovalHarness harness = await RemovalHarness.CreateWithDueBindingAsync();
        ApplicationResult<PolicyBindingView> result = await harness.Expire.ExecuteAsync(
            new ExpireIncidentDenyOverlayBindingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                BindingId = harness.BindingId,
                ExpectedRowVersion = harness.BindingRowVersion,
            });
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PolicyBindingState.ExpiredPendingReconciliation, result.Value!.State);
        Assert.False(result.Value.DeploymentStarted);
        Assert.Contains(
            harness.Audit.Events,
            e => e.Action == ExpireIncidentDenyOverlayBindingUseCase.Operation
                 && e.PayloadJson.Contains("\"deployment_started\":false", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac7CompileAfterExpireExcludesOverlayRules()
    {
        RemovalHarness harness = await RemovalHarness.CreateWithDueBindingAsync();
        ApplicationResult<PolicyBindingView> expired = await harness.Expire.ExecuteAsync(
            new ExpireIncidentDenyOverlayBindingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                BindingId = harness.BindingId,
                ExpectedRowVersion = harness.BindingRowVersion,
            });
        Assert.True(expired.IsSuccess, expired.Error?.Message);

        ApplicationResult<CompileNodeFilterArtifactsView> compiled = await harness.Compile.ExecuteAsync(
            new CompileNodeFilterArtifactsCommand
            {
                Actor = "tester",
                NodeId = harness.NodeId,
                AnalysisRunId = harness.AnalysisRunId,
                CurrentDependencyFingerprint = harness.Fingerprint,
                CurrentCapabilityHash = CapabilityHashBytes,
            });
        Assert.True(compiled.IsSuccess, compiled.Error?.Message);
        Assert.True(compiled.Value!.Artifacts[0].RuleCount < harness.BaselineRuleCount);
    }

    [Fact]
    public async Task Ac8PlanRemovalCreatesPlanWithoutStartDeployment()
    {
        RemovalHarness harness = await RemovalHarness.CreateWithDueBindingAsync();
        ApplicationResult<PlanIncidentDenyOverlayRemovalView> result = await harness.Plan.ExecuteAsync(
            harness.PlanCommand());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEqual(Guid.Empty, result.Value!.PlanId);
        Assert.NotEmpty(result.Value.Artifacts);
        Assert.Contains(harness.Audit.Events, e => e.Action == PlanIncidentDenyOverlayRemovalUseCase.Operation);
        Assert.Contains(harness.Audit.Events, e => e.Action == CreateDeploymentPlanUseCase.Operation);
        Assert.DoesNotContain(harness.Audit.Events, e => e.Action == StartDeploymentUseCase.Operation);
    }

    [Fact]
    public async Task Ac9PlanAuditRecordsDeploymentStartedFalse()
    {
        RemovalHarness harness = await RemovalHarness.CreateWithDueBindingAsync();
        ApplicationResult<PlanIncidentDenyOverlayRemovalView> result = await harness.Plan.ExecuteAsync(
            harness.PlanCommand());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(
            harness.Audit.Events,
            e => e.Action == PlanIncidentDenyOverlayRemovalUseCase.Operation
                 && e.PayloadJson.Contains("\"deployment_started\":false", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac10PlanRejectsUnauthorizedActor()
    {
        RemovalHarness harness = await RemovalHarness.CreateWithDueBindingAsync();
        harness.Auth.DeniedPermissions.Add(ApplicationPermissions.IncidentOverlayRemove);
        ApplicationResult<PlanIncidentDenyOverlayRemovalView> result = await harness.Plan.ExecuteAsync(
            harness.PlanCommand());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Ac11ReconcileJobExpiresDueOverlayBindingsWithoutRouterOs()
    {
        RemovalHarness harness = await RemovalHarness.CreateWithDueBindingAsync();
        ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase job = new(
            harness.Approvals,
            harness.Expire,
            harness.Clock);
        ApplicationResult<ReconcileExpiredIncidentDenyOverlayBindingsJobResult> result = await job.ExecuteAsync(
            "system:jobs",
            batchSize: 8);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(harness.BindingId, result.Value!.ExpiredBindingIds);
        PolicyDesiredBinding? updated = await harness.Approvals.GetBindingAsync(new PolicyBindingId(harness.BindingId));
        Assert.NotNull(updated);
        Assert.Equal(PolicyBindingState.ExpiredPendingReconciliation, updated.State);
    }

    private static PolicyDesiredBinding SampleBinding(PolicyBindingScope scope, DateTimeOffset validUntil)
        => PolicyDesiredBinding.Reconstitute(
            PolicyBindingId.New(),
            scope,
            NodeId,
            PolicyId.New(),
            PolicyRevisionId.New(),
            PolicyAnalysisRunId.New(),
            DeploymentTestFactory.H("bundle"),
            PolicyBindingState.Active,
            validFromUtc: validUntil.AddHours(-2),
            validUntilUtc: validUntil,
            rowVersion: 1,
            validUntil.AddHours(-2),
            validUntil.AddHours(-2));

    private sealed class RemovalHarness
    {
        public FakeAuthorizationBoundary Auth { get; }

        public FakeAuditEventWriter Audit { get; }

        public FakePolicyApprovalStore Approvals { get; }

        public FakeClock Clock { get; }

        public ExpireIncidentDenyOverlayBindingUseCase Expire { get; }

        public PlanIncidentDenyOverlayRemovalUseCase Plan { get; }

        public CompileNodeFilterArtifactsUseCase Compile { get; }

        public Guid NodeId { get; }

        public Guid OverlayPolicyId { get; }

        public Guid BindingId { get; }

        public ulong BindingRowVersion { get; }

        public Guid AnalysisRunId { get; }

        public byte[] Fingerprint { get; }

        public int BaselineRuleCount { get; }

        public DomainNode Node { get; }

        private RemovalHarness(
            FakeAuthorizationBoundary auth,
            FakeAuditEventWriter audit,
            FakePolicyApprovalStore approvals,
            FakeClock clock,
            ExpireIncidentDenyOverlayBindingUseCase expire,
            PlanIncidentDenyOverlayRemovalUseCase plan,
            CompileNodeFilterArtifactsUseCase compile,
            Guid nodeId,
            Guid overlayPolicyId,
            Guid bindingId,
            ulong bindingRowVersion,
            Guid analysisRunId,
            byte[] fingerprint,
            int baselineRuleCount,
            DomainNode node)
        {
            Auth = auth;
            Audit = audit;
            Approvals = approvals;
            Clock = clock;
            Expire = expire;
            Plan = plan;
            Compile = compile;
            NodeId = nodeId;
            OverlayPolicyId = overlayPolicyId;
            BindingId = bindingId;
            BindingRowVersion = bindingRowVersion;
            AnalysisRunId = analysisRunId;
            Fingerprint = fingerprint;
            BaselineRuleCount = baselineRuleCount;
            Node = node;
        }

        public static async Task<RemovalHarness> CreateWithDueBindingAsync()
        {
            CompileNodeFilterArtifactsUseCaseTests.CompileFixture fx =
                await SeedCompileFixtureAsync(bindOverlay: true);
            FakeAuthorizationBoundary auth = new();
            FakeDeploymentStore deployments = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new() { UtcNow = T0 };
            FakeResponseFeedbackEventStore feedbackStore = new();
            EmitResponseFeedbackUseCase feedback = ResponseFeedbackTestFactory.CreateEmit(auth, feedbackStore, audit, clock);
            ExpireIncidentDenyOverlayBindingUseCase expire = new(auth, fx.Approvals, idempotency, audit, clock, fx.Policies, feedback);
            CreateDeploymentPlanUseCase createPlan = new(
                auth, fx.Nodes, deployments, idempotency, audit, clock,
                new Mfc.Application.Topology.VrrpPairConsistencyLoader(
                    new FakeDeviceStore(), new FakeSnapshotStore(), new FakeDeviceHashStateStore()),
                new FakeUnitOfWork());
            PlanIncidentDenyOverlayRemovalUseCase plan = new(
                auth, fx.Policies, fx.Approvals, audit, expire, fx.UseCase, createPlan, feedback);
            DomainPolicy overlay = (await fx.Policies.ListActiveByOwnerAsync(
                PolicyKind.IncidentDenyOverlay,
                fx.NodeId)).Single();
            PolicyDesiredBinding binding = (await fx.Approvals.ListActiveBindingsAsync(
                PolicyBindingScope.IncidentDenyOverlay,
                fx.NodeId)).Single(b => b.PolicyId == overlay.Id);
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
                ExpiredAt,
                binding.RowVersion,
                binding.CreatedAtUtc,
                binding.UpdatedAtUtc);
            await fx.Approvals.SaveBindingAsync(binding);

            ApplicationResult<CompileNodeFilterArtifactsView> baseline = await fx.UseCase.ExecuteAsync(
                new CompileNodeFilterArtifactsCommand
                {
                    Actor = "tester",
                    NodeId = fx.NodeId,
                    AnalysisRunId = fx.RunId,
                    CurrentDependencyFingerprint = fx.Fingerprint,
                    CurrentCapabilityHash = CapabilityHashBytes,
                });
            Assert.True(baseline.IsSuccess, baseline.Error?.Message);
            DomainNode? node = await fx.Nodes.GetAsync(new NodeId(fx.NodeId));
            Assert.NotNull(node);
            return new RemovalHarness(
                auth,
                audit,
                fx.Approvals,
                clock,
                expire,
                plan,
                fx.UseCase,
                fx.NodeId,
                overlay.Id.Value,
                binding.Id.Value,
                binding.RowVersion,
                fx.RunId,
                fx.Fingerprint,
                baseline.Value!.Artifacts[0].RuleCount,
                node);
        }

        public PlanIncidentDenyOverlayRemovalCommand PlanCommand()
            => new()
            {
                Actor = "tester",
                NodeId = NodeId,
                OverlayPolicyId = OverlayPolicyId,
                BindingId = BindingId,
                ExpectedBindingRowVersion = BindingRowVersion,
                ExpireIdempotencyKey = Guid.NewGuid(),
                PlanIdempotencyKey = Guid.NewGuid(),
                AnalysisRunId = AnalysisRunId,
                CurrentDependencyFingerprint = Fingerprint,
                CurrentCapabilityHash = CapabilityHashBytes,
                LogicalPolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
                AnalysisBundleHash = DeploymentTestFactory.H("analysis").Bytes.ToArray(),
                TopologyProjectionHash = DeploymentTestFactory.H("topology").Bytes.ToArray(),
                DevicePlans = [DeploymentTestFactory.DevicePlan(Node.Devices[0].Id, Node.DeclaredKind)],
            };
    }

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
}
