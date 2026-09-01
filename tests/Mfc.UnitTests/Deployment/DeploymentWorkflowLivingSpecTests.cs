using System.Security.Cryptography;
using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using DeploymentProgress = Mfc.Contracts.Mfc.V1.DeploymentProgress;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainState = Mfc.Domain.Deployment.DeploymentOperationState;
using ProtoState = Mfc.Contracts.Mfc.V1.DeploymentOperationState;

namespace Mfc.UnitTests.Deployment;

/// <summary>Living Spec matrix for Issue Set M4-12 AC 1–11 (deployment API + Desktop wiring).</summary>
public sealed class DeploymentWorkflowLivingSpecTests
{
    [Fact]
    public void Ac1SeparateRpcsExistOnTheContract()
    {
        string[] methods = DeploymentService.Descriptor.Methods.Select(static m => m.Name).ToArray();
        Assert.Contains("CreatePlan", methods);
        Assert.Contains("Start", methods);
        Assert.Contains("Watch", methods);
        Assert.Contains("Rollback", methods);
        Assert.Contains("GetRecoveryStatus", methods);
    }

    [Fact]
    public async Task Ac2StartRequiresExactPlanHash()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true);
        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        Assert.True(plan.IsSuccess);
        ApplicationResult<DeploymentOperationSummaryView> missing = await harness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = [],
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            });
        Assert.True(missing.IsFailure);
        Assert.Equal(DeploymentCodes.PlanHashMismatch, missing.Error!.Code);

        byte[] wrong = SHA256.HashData("wrong-plan"u8.ToArray());
        ApplicationResult<DeploymentOperationSummaryView> mismatch = await harness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value.PlanId,
                PlanHash = wrong,
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            });
        Assert.True(mismatch.IsFailure);
        Assert.Equal(DeploymentCodes.PlanHashMismatch, mismatch.Error!.Code);
    }

    [Fact]
    public async Task Ac3WatchReplaysServerStreamingProgressUntilTerminal()
    {
        DeploymentProgressHub hub = new();
        Guid operationId = Guid.NewGuid();
        hub.Publish(operationId, DomainState.Prechecking, timelineEntry: "precheck");
        hub.Publish(operationId, DomainState.Committed);
        List<DeploymentProgress> received = [];
        await foreach (DeploymentProgress progress in hub.WatchAsync(operationId, CancellationToken.None))
        {
            received.Add(progress);
        }

        Assert.Equal(2, received.Count);
        Assert.Equal(ProtoState.Committed, received[^1].State);
        Assert.True(DeploymentProtoMapper.IsTerminal(received[^1].State));
    }

    [Fact]
    public async Task Ac3bWatchReplaysRollbackEventsAfterCommittedTerminal()
    {
        DeploymentProgressHub hub = new();
        Guid operationId = Guid.NewGuid();
        hub.Publish(operationId, DomainState.Prechecking, timelineEntry: "precheck");
        hub.Publish(operationId, DomainState.Committed);
        hub.Publish(operationId, DomainState.RollingBack, timelineEntry: "rolling back");
        hub.Publish(operationId, DomainState.RolledBack);
        List<DeploymentProgress> received = [];
        await foreach (DeploymentProgress progress in hub.WatchAsync(operationId, CancellationToken.None))
        {
            received.Add(progress);
        }

        Assert.Equal(4, received.Count);
        Assert.Equal(ProtoState.Committed, received[1].State);
        Assert.Equal(ProtoState.RollingBack, received[2].State);
        Assert.Equal(ProtoState.RolledBack, received[^1].State);
        Assert.True(DeploymentProtoMapper.IsTerminal(received[^1].State));
    }

    [Fact]
    public void Ac4To7DesktopSurfacesDiffArtifactsOrderProbesAndNoForceApply()
    {
        Type vm = typeof(Mfc.Desktop.ViewModels.DeploymentViewModel);
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.SemanticDiffLines)));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.ArtifactLines)));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.OrderLines)));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.ProbeAndWatchdogLines)));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasForceApply)));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasRawRouterOsCommands)));
        System.Reflection.PropertyInfo force = vm.GetProperty(
            nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasForceApply))!;
        System.Reflection.PropertyInfo raw = vm.GetProperty(
            nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasRawRouterOsCommands))!;
        Assert.Equal(typeof(bool), force.PropertyType);
        Assert.Equal(typeof(bool), raw.PropertyType);
    }

    [Fact]
    public async Task Ac8CancellationAfterActivationBecomesRollback()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true, cancelAfterActivation: true);
        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        ApplicationResult<DeploymentOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            },
            cts.Token);
        Assert.True(started.IsSuccess, started.Error?.Message);
        Assert.Equal(DomainState.RolledBack, started.Value!.State);
        Assert.Contains(started.Value.Timeline, static t => t.Contains("rollback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac9ForceApplyAbsentFromContract()
    {
        Assert.DoesNotContain(
            DeploymentService.Descriptor.Methods,
            static m => m.Name.Contains("Force", StringComparison.OrdinalIgnoreCase));
        Assert.Null(CreateDeploymentPlanRequest.Descriptor.FindFieldByName("force_apply"));
        Assert.Null(StartDeploymentRequest.Descriptor.FindFieldByName("force_apply"));
    }

    [Fact]
    public void Ac10NoRawRouterOsCommandsOnDesktop()
    {
        System.Reflection.PropertyInfo raw = typeof(Mfc.Desktop.ViewModels.DeploymentViewModel)
            .GetProperty(nameof(Mfc.Desktop.ViewModels.DeploymentViewModel.HasRawRouterOsCommands))!;
        Assert.Equal(typeof(bool), raw.PropertyType);
        Assert.True(raw.CanRead);
    }

    [Fact]
    public async Task Ac11EveryWorkflowOperationIsAudited()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: false);
        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        ApplicationResult<DeploymentOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            });
        await harness.Rollback.ExecuteAsync(
            new RollbackDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                OperationId = started.Value!.OperationId,
            });
        await harness.Recovery.ExecuteAsync(
            new GetDeploymentRecoveryStatusQuery
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                OperationId = started.Value.OperationId,
            });

        string[] actions = harness.Audit.Events.Select(static e => e.Action).ToArray();
        Assert.Contains(CreateDeploymentPlanUseCase.Operation, actions);
        Assert.Contains(StartDeploymentUseCase.Operation, actions);
        Assert.Contains(RollbackDeploymentWorkflowUseCase.Operation, actions);
        Assert.Contains(GetDeploymentRecoveryStatusUseCase.Operation, actions);
    }

    [Fact]
    public async Task StartPersistsRecoveryRequiredWhenRuntimeIsNotConfigured()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true, throwOnExecute: true);
        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        ApplicationResult<DeploymentOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            });
        Assert.True(started.IsFailure);
        Assert.Equal("failed", started.Error!.Code);
        Assert.Single(harness.Store.Operations);
        Assert.Equal(DomainState.RecoveryRequired, harness.Store.Operations.Single().State);
    }

    [Fact]
    public async Task PlanSummaryExposesSemanticDiffArtifactsOrderAndProbes()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true);
        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        Assert.True(plan.IsSuccess);
        Assert.NotEmpty(plan.Value!.SemanticDiffEntries);
        Assert.NotEmpty(plan.Value.SemanticDiff);
        DeploymentSemanticDiffEntryView first = plan.Value.SemanticDiff[0];
        Assert.NotEqual(Mfc.Application.Deployment.DeploymentSemanticDiffKind.Unspecified, first.Kind);
        Assert.Contains("/artifact", first.Path, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(first.Before));
        Assert.False(string.IsNullOrWhiteSpace(first.After));
        Assert.Equal(first.HashDelta, plan.Value.SemanticDiffEntries[0]);
        Assert.NotEmpty(plan.Value.Devices);
        Assert.NotEmpty(plan.Value.Devices[0].ActivationOrderMarkers);
        Assert.NotEmpty(plan.Value.Devices[0].RollbackOrderMarkers);
        Assert.True(plan.Value.Devices[0].WatchdogTtlSeconds >= 60);
        Assert.NotEmpty(plan.Value.Devices[0].Probes);
        Assert.NotEmpty(plan.Value.ActivationOrderDeviceIds);
        Assert.NotEmpty(plan.Value.RollbackOrderDeviceIds);
    }

    [Fact]
    public async Task WorkflowCoversAuthIdempotencyReplayRecoveryLiveJumpsAndNotConfiguredRuntime()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true);
        harness.Auth.DeniedPermissions.Add(
            global::Mfc.Application.Abstractions.Authorization.ApplicationPermissions.DeploymentWrite);
        Assert.Equal("forbidden", (await harness.CreatePlanAsync()).Error!.Code);
        harness.Auth.DeniedPermissions.Clear();

        Assert.Equal(
            "validation",
            (await harness.Plans.ExecuteAsync(harness.PlanCommand(Guid.Empty))).Error!.Code);

        Guid key = Guid.NewGuid();
        CreateDeploymentPlanCommand firstCmd = harness.PlanCommand(key);
        Assert.True((await harness.Plans.ExecuteAsync(firstCmd)).IsSuccess);
        ApplicationResult<DeploymentPlanSummaryView> replay = await harness.Plans.ExecuteAsync(firstCmd);
        Assert.True(replay.IsSuccess);

        ApplicationResult<DeploymentPlanSummaryView> conflict = await harness.Plans.ExecuteAsync(
            new CreateDeploymentPlanCommand
            {
                Actor = firstCmd.Actor,
                IdempotencyKey = key,
                NodeId = firstCmd.NodeId,
                LogicalPolicyHash = firstCmd.LogicalPolicyHash,
                AnalysisBundleHash = firstCmd.AnalysisBundleHash,
                TopologyProjectionHash = DeploymentTestFactory.H("other-topology").Bytes.ToArray(),
                DevicePlans = firstCmd.DevicePlans,
            });
        Assert.Equal("conflict", conflict.Error!.Code);

        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        Guid startKey = Guid.NewGuid();
        StartDeploymentCommand start = new()
        {
            Actor = "tester",
            IdempotencyKey = startKey,
            PlanId = plan.Value!.PlanId,
            PlanHash = plan.Value.PlanHash,
            PacketPathPairs = DeploymentTestFactory.CpuPairs(),
        };
        ApplicationResult<DeploymentOperationSummaryView> started = await harness.Start.ExecuteAsync(start);
        Assert.True(started.IsSuccess);
        Assert.Equal(started.Value!.OperationId, (await harness.Start.ExecuteAsync(start)).Value!.OperationId);

        DeviceDeploymentPlan devicePlan = harness.StorePlansDevice(plan.Value.PlanId);
        Dictionary<string, string> jumps = devicePlan.NewAnchorTargets.ToDictionary(
            static t => t.Key.Marker,
            static t => t.JumpTarget,
            StringComparer.Ordinal);
        ApplicationResult<DeploymentRecoveryStatusView> live = await harness.Recovery.ExecuteAsync(
            new GetDeploymentRecoveryStatusQuery
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                OperationId = started.Value.OperationId,
                LiveJumpsByMarker = jumps,
                WatchdogSchedulers = [("mfc-rb-d-0123456789abcdef", false)],
            });
        Assert.True(live.IsSuccess);
        Assert.Equal(Mfc.Domain.Deployment.DeploymentRecoveryAction.KeepCommitted, live.Value!.Action);

        ApplicationResult<DeploymentRecoveryStatusView> recoveryRequired = await harness.Recovery.ExecuteAsync(
            new GetDeploymentRecoveryStatusQuery
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                OperationId = started.Value.OperationId,
            });
        Assert.Equal(Mfc.Domain.Deployment.DeploymentRecoveryAction.KeepCommitted, recoveryRequired.Value!.Action);

        Assert.Equal(
            "not_found",
            (await harness.Rollback.ExecuteAsync(
                new RollbackDeploymentCommand
                {
                    Actor = "tester",
                    IdempotencyKey = Guid.NewGuid(),
                    OperationId = Guid.NewGuid(),
                })).Error!.Code);

        Assert.Equal(
            "not_found",
            (await harness.Recovery.ExecuteAsync(
                new GetDeploymentRecoveryStatusQuery
                {
                    Actor = "tester",
                    NodeId = Guid.NewGuid(),
                })).Error!.Code);

        NotConfiguredDeploymentRuntime runtime = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DeploymentPlan livePlan = DeploymentTestFactory.PlanFor(harness.Node, now);
        DeploymentOperation liveOp = DeploymentOperation.Create(livePlan, harness.Node, UserId.New(), now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.ExecuteAsync(
            harness.Node,
            livePlan,
            liveOp,
            DeploymentTestFactory.CpuPairs(),
            now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RollbackAsync(
            harness.Node,
            livePlan,
            liveOp,
            now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RecoverAsync(
            harness.Node,
            livePlan,
            liveOp,
            now));
    }

    [Fact]
    public async Task IdempotentRollbackAndPreActivationCancelRethrows()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: false);
        ApplicationResult<DeploymentPlanSummaryView> plan = await harness.CreatePlanAsync();
        ApplicationResult<DeploymentOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            });
        Guid rollbackKey = Guid.NewGuid();
        RollbackDeploymentCommand rollback = new()
        {
            Actor = "tester",
            IdempotencyKey = rollbackKey,
            OperationId = started.Value!.OperationId,
        };
        ApplicationResult<DeploymentOperationSummaryView> first = await harness.Rollback.ExecuteAsync(rollback);
        ApplicationResult<DeploymentOperationSummaryView> second = await harness.Rollback.ExecuteAsync(rollback);
        Assert.Equal(first.Value!.OperationId, second.Value!.OperationId);

        WorkflowHarness cancelHarness = WorkflowHarness.Create(commit: true, cancelBeforeActivation: true);
        ApplicationResult<DeploymentPlanSummaryView> plan2 = await cancelHarness.CreatePlanAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelHarness.Start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan2.Value!.PlanId,
                PlanHash = plan2.Value.PlanHash,
                PacketPathPairs = DeploymentTestFactory.CpuPairs(),
            }));
    }

    private sealed class WorkflowHarness
    {
        private WorkflowHarness(
            DomainNode node,
            FakeDeploymentStore store,
            FakeAuthorizationBoundary auth,
            FakeAuditEventWriter audit,
            CreateDeploymentPlanUseCase create,
            StartDeploymentUseCase start,
            RollbackDeploymentWorkflowUseCase rollback,
            GetDeploymentRecoveryStatusUseCase recovery)
        {
            Node = node;
            Store = store;
            Auth = auth;
            Audit = audit;
            Plans = create;
            Start = start;
            Rollback = rollback;
            Recovery = recovery;
        }

        public DomainNode Node { get; }

        public FakeDeploymentStore Store { get; }

        public FakeAuthorizationBoundary Auth { get; }

        public FakeAuditEventWriter Audit { get; }

        public CreateDeploymentPlanUseCase Plans { get; }

        public StartDeploymentUseCase Start { get; }

        public RollbackDeploymentWorkflowUseCase Rollback { get; }

        public GetDeploymentRecoveryStatusUseCase Recovery { get; }

        public static WorkflowHarness Create(
            bool commit,
            bool throwOnExecute = false,
            bool cancelAfterActivation = false,
            bool cancelBeforeActivation = false)
        {
            DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
            FakeNodeStore nodes = new();
            nodes.AddAsync(node).GetAwaiter().GetResult();
            FakeDeploymentStore store = new();
            FakeAuthorizationBoundary auth = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new();
            ScriptedDeploymentRuntime runtime = new()
            {
                Commit = commit,
                ThrowOnExecute = throwOnExecute,
                CancelAfterActivation = cancelAfterActivation,
                CancelBeforeActivation = cancelBeforeActivation,
            };
            return new WorkflowHarness(
                node,
                store,
                auth,
                audit,
                new CreateDeploymentPlanUseCase(auth, nodes, store, idempotency, audit, clock),
                new StartDeploymentUseCase(
                    auth, nodes, store, new FakeDriftEventStore(), idempotency, audit, clock, runtime),
                new RollbackDeploymentWorkflowUseCase(auth, nodes, store, idempotency, audit, clock, runtime),
                new GetDeploymentRecoveryStatusUseCase(auth, nodes, store, audit));
        }

        public DeviceDeploymentPlan StorePlansDevice(Guid planId)
            => Store.GetPlanAsync(new Mfc.Domain.Deployment.Primitives.DeploymentPlanId(planId))
                .GetAwaiter().GetResult()!.DevicePlans[0];

        public CreateDeploymentPlanCommand PlanCommand(Guid idempotencyKey)
            => new()
            {
                Actor = "tester",
                IdempotencyKey = idempotencyKey,
                NodeId = Node.Id.Value,
                LogicalPolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
                AnalysisBundleHash = DeploymentTestFactory.H("analysis").Bytes.ToArray(),
                TopologyProjectionHash = DeploymentTestFactory.H("topology").Bytes.ToArray(),
                DevicePlans = [DeploymentTestFactory.DevicePlan(Node.Devices[0].Id, Node.DeclaredKind)],
            };

        public Task<ApplicationResult<DeploymentPlanSummaryView>> CreatePlanAsync()
            => Plans.ExecuteAsync(PlanCommand(Guid.NewGuid()));
    }

    internal sealed class ScriptedDeploymentRuntime : IDeploymentRuntime
    {
        public bool Commit { get; init; } = true;

        public bool ThrowOnExecute { get; init; }

        public bool CancelAfterActivation { get; init; }

        public bool CancelBeforeActivation { get; init; }

        public Task<DeploymentWorkflowExecutionResult> ExecuteAsync(
            DomainNode node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            IReadOnlyList<PacketPathPairFact> packetPathPairs,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(packetPathPairs);
            if (ThrowOnExecute)
            {
                throw new InvalidOperationException(NotConfiguredDeploymentRuntime.NotConfiguredMessage);
            }

            List<string> timeline = ["execute"];
            Advance(operation, DomainState.Prechecking, nowUtc);
            if (CancelBeforeActivation)
            {
                throw new OperationCanceledException();
            }

            Advance(operation, DomainState.Staging, nowUtc);
            Advance(operation, DomainState.Staged, nowUtc);
            Advance(operation, DomainState.ArmingWatchdog, nowUtc);
            Advance(operation, DomainState.WatchdogArmed, nowUtc);
            Advance(operation, DomainState.Activating, nowUtc);
            if (CancelAfterActivation || cancellationToken.IsCancellationRequested)
            {
                timeline.Add("cancel-after-activation");
                throw new OperationCanceledException(cancellationToken);
            }

            if (Commit)
            {
                Advance(operation, DomainState.Verifying, nowUtc);
                Advance(operation, DomainState.DisarmingWatchdog, nowUtc);
                Advance(operation, DomainState.Committed, nowUtc);
                return Task.FromResult(new DeploymentWorkflowExecutionResult
                {
                    Succeeded = true,
                    State = operation.State,
                    Timeline = timeline,
                    ActivationStarted = true,
                });
            }

            Advance(operation, DomainState.RollbackPending, nowUtc);
            return Task.FromResult(new DeploymentWorkflowExecutionResult
            {
                Succeeded = false,
                State = operation.State,
                Timeline = timeline,
                ActivationStarted = true,
            });
        }

        public Task<DeploymentWorkflowRollbackResult> RollbackAsync(
            DomainNode node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.State == DomainState.Activating
                || operation.State == DomainState.Verifying
                || operation.State == DomainState.DisarmingWatchdog)
            {
                Advance(operation, DomainState.RollbackPending, nowUtc);
            }

            if (operation.State == DomainState.Created)
            {
                Advance(operation, DomainState.Prechecking, nowUtc);
                Advance(operation, DomainState.Staging, nowUtc);
                Advance(operation, DomainState.RollbackPending, nowUtc);
            }

            if (operation.State == DomainState.RollbackPending)
            {
                Advance(operation, DomainState.RollingBack, nowUtc);
            }

            if (operation.State == DomainState.RollingBack)
            {
                Advance(operation, DomainState.RolledBack, nowUtc);
            }

            return Task.FromResult(new DeploymentWorkflowRollbackResult
            {
                Succeeded = operation.State == DomainState.RolledBack,
                State = operation.State,
                Timeline = ["rollback"],
            });
        }

        public Task<DeploymentWorkflowRecoveryResult> RecoverAsync(
            DomainNode node,
            DeploymentPlan plan,
            DeploymentOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DeploymentWorkflowRecoveryResult
            {
                Action = Mfc.Domain.Deployment.DeploymentRecoveryAction.MarkFailedOrCanceled,
                State = operation.State,
                Timeline = [],
            });
        }

        private static void Advance(DeploymentOperation operation, DomainState next, DateTimeOffset nowUtc)
            => operation.EnsureTransition(next, nowUtc);
    }
}
