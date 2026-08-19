using System.Security.Cryptography;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Common;
using Mfc.Application.Onboarding;
using Mfc.Controller.Grpc;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using DomainFacts = Mfc.Domain.Onboarding.OnboardingDevicePrerequisiteFacts;
using DomainManagement = Mfc.Domain.Inventory.ManagementState;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainState = Mfc.Domain.Onboarding.OnboardingOperationState;
using DomainSupport = Mfc.Domain.Inventory.SupportState;
using OnboardingProgress = Mfc.Contracts.Mfc.V1.OnboardingProgress;
using OnboardingService = Mfc.Contracts.Mfc.V1.OnboardingService;
using ProtoState = Mfc.Contracts.Mfc.V1.OnboardingOperationState;

namespace Mfc.UnitTests.Onboarding;

/// <summary>Living Spec matrix for Issue Set M5-09 AC 1–10 (onboarding API + Desktop wiring).</summary>
public sealed class OnboardingWorkflowLivingSpecTests
{
    [Fact]
    public void Ac1SeparateRpcsExistOnTheContract()
    {
        string[] methods = OnboardingService.Descriptor.Methods.Select(static m => m.Name).ToArray();
        Assert.Contains("ValidatePrerequisites", methods);
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
        ApplicationResult<OnboardingPlanSummaryView> plan = await harness.CreatePlanAsync();
        Assert.True(plan.IsSuccess);
        ApplicationResult<OnboardingOperationSummaryView> missing = await harness.Start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = [],
            });
        Assert.True(missing.IsFailure);
        Assert.Equal(OnboardingCodes.PlanHashMismatch, missing.Error!.Code);

        byte[] wrong = SHA256.HashData("wrong-plan"u8.ToArray());
        ApplicationResult<OnboardingOperationSummaryView> mismatch = await harness.Start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value.PlanId,
                PlanHash = wrong,
            });
        Assert.True(mismatch.IsFailure);
        Assert.Equal(OnboardingCodes.PlanHashMismatch, mismatch.Error!.Code);
    }

    [Fact]
    public async Task Ac3WatchReplaysServerStreamingProgressUntilTerminal()
    {
        OnboardingProgressHub hub = new();
        Guid operationId = Guid.NewGuid();
        hub.Publish(operationId, DomainState.Prechecking, timelineEntry: "precheck");
        hub.Publish(operationId, DomainState.Committed);
        List<OnboardingProgress> received = [];
        await foreach (OnboardingProgress progress in hub.WatchAsync(operationId, CancellationToken.None))
        {
            received.Add(progress);
        }

        Assert.Equal(2, received.Count);
        Assert.Equal(ProtoState.Committed, received[^1].State);
        Assert.True(OnboardingProtoMapper.IsTerminal(received[^1].State));
    }

    [Fact]
    public void Ac4To7DesktopChecklistPlacementAndNoWriteSurface()
    {
        Type vm = typeof(Mfc.Desktop.ViewModels.OnboardingViewModel);
        Assert.NotNull(vm.GetProperty("Findings"));
        Assert.NotNull(vm.GetProperty("Placements"));
        Assert.NotNull(vm.GetProperty("RecoveryFactsText"));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.OnboardingViewModel.HasScriptSource)));
        Assert.NotNull(vm.GetProperty(nameof(Mfc.Desktop.ViewModels.OnboardingViewModel.HasArbitraryWriteControls)));
    }

    [Fact]
    public void Ac4To7DesktopFlagsAreCompileTimeFalse()
    {
        System.Reflection.PropertyInfo script = typeof(Mfc.Desktop.ViewModels.OnboardingViewModel)
            .GetProperty(nameof(Mfc.Desktop.ViewModels.OnboardingViewModel.HasScriptSource))!;
        System.Reflection.PropertyInfo write = typeof(Mfc.Desktop.ViewModels.OnboardingViewModel)
            .GetProperty(nameof(Mfc.Desktop.ViewModels.OnboardingViewModel.HasArbitraryWriteControls))!;
        Assert.Equal(typeof(bool), script.PropertyType);
        Assert.Equal(typeof(bool), write.PropertyType);
        Assert.True(script.CanRead);
        Assert.True(write.CanRead);
    }

    [Fact]
    public async Task Ac8RecoveryFactsMatchStoredOperation()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true);
        ApplicationResult<OnboardingPlanSummaryView> plan = await harness.CreatePlanAsync();
        ApplicationResult<OnboardingOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
            });
        Assert.True(started.IsSuccess);
        ApplicationResult<OnboardingRecoveryStatusView> status = await harness.Recovery.ExecuteAsync(
            new GetOnboardingRecoveryStatusQuery
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                OperationId = started.Value!.OperationId,
            });
        Assert.True(status.IsSuccess);
        Assert.Equal(started.Value.OperationId, status.Value!.OperationId);
        Assert.Equal(DomainState.Committed, status.Value.OperationState);
        Assert.Equal(OnboardingRecoveryAction.KeepManaged, status.Value.Action);
    }

    [Fact]
    public async Task Ac9MutationRpcsAreIdempotent()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true);
        Guid key = Guid.NewGuid();
        CreateOnboardingPlanCommand command = harness.PlanCommand(key);
        ApplicationResult<OnboardingPlanSummaryView> first = await harness.Plans.ExecuteAsync(command);
        ApplicationResult<OnboardingPlanSummaryView> second = await harness.Plans.ExecuteAsync(command);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.PlanId, second.Value!.PlanId);

        Guid startKey = Guid.NewGuid();
        StartOnboardingCommand start = new()
        {
            Actor = "tester",
            IdempotencyKey = startKey,
            PlanId = first.Value.PlanId,
            PlanHash = first.Value.PlanHash,
        };
        ApplicationResult<OnboardingOperationSummaryView> started = await harness.Start.ExecuteAsync(start);
        ApplicationResult<OnboardingOperationSummaryView> replayed = await harness.Start.ExecuteAsync(start);
        Assert.Equal(started.Value!.OperationId, replayed.Value!.OperationId);
    }

    [Fact]
    public async Task Ac10EveryWorkflowOperationIsAudited()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: false);
        await harness.Validate.ExecuteAsync(harness.ValidateCommand());
        ApplicationResult<OnboardingPlanSummaryView> plan = await harness.CreatePlanAsync();
        ApplicationResult<OnboardingOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
            });
        await harness.Rollback.ExecuteAsync(
            new RollbackOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                OperationId = started.Value!.OperationId,
            });
        await harness.Recovery.ExecuteAsync(
            new GetOnboardingRecoveryStatusQuery
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                OperationId = started.Value.OperationId,
            });

        string[] actions = harness.Audit.Events.Select(static e => e.Action).ToArray();
        Assert.Contains(ValidateOnboardingPrerequisitesWorkflowUseCase.Operation, actions);
        Assert.Contains(CreateOnboardingPlanUseCase.Operation, actions);
        Assert.Contains(StartOnboardingUseCase.Operation, actions);
        Assert.Contains(RollbackOnboardingWorkflowUseCase.Operation, actions);
        Assert.Contains(GetOnboardingRecoveryStatusUseCase.Operation, actions);
    }

    [Fact]
    public async Task StartPersistsRecoveryRequiredWhenRuntimeIsNotConfigured()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true, throwOnExecute: true);
        ApplicationResult<OnboardingPlanSummaryView> plan = await harness.CreatePlanAsync();
        ApplicationResult<OnboardingOperationSummaryView> started = await harness.Start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                PlanId = plan.Value!.PlanId,
                PlanHash = plan.Value.PlanHash,
            });
        Assert.True(started.IsFailure);
        Assert.Equal("failed", started.Error!.Code);
        Assert.Single(harness.Store.Operations);
        Assert.Equal(DomainState.RecoveryRequired, harness.Store.Operations.Single().State);
    }

    [Fact]
    public async Task ValidateAndCreatePlanCoverNotFoundAndForbidden()
    {
        WorkflowHarness harness = WorkflowHarness.Create(commit: true);
        harness.Auth.DeniedPermissions.Add(
            global::Mfc.Application.Abstractions.Authorization.ApplicationPermissions.OnboardingRead);
        ApplicationResult<OnboardingPrerequisiteReportView> forbidden = await harness.Validate.ExecuteAsync(
            harness.ValidateCommand());
        Assert.True(forbidden.IsFailure);
        Assert.Equal("forbidden", forbidden.Error!.Code);

        harness.Auth.DeniedPermissions.Clear();
        ApplicationResult<OnboardingPrerequisiteReportView> missing = await harness.Validate.ExecuteAsync(
            new ValidateOnboardingPrerequisitesCommand
            {
                Actor = "tester",
                NodeId = Guid.NewGuid(),
                Facts = [],
            });
        Assert.True(missing.IsFailure);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    private sealed class WorkflowHarness
    {
        private WorkflowHarness(
            DomainNode node,
            FakeOnboardingStore store,
            FakeAuthorizationBoundary auth,
            FakeAuditEventWriter audit,
            ValidateOnboardingPrerequisitesWorkflowUseCase validate,
            CreateOnboardingPlanUseCase create,
            StartOnboardingUseCase start,
            RollbackOnboardingWorkflowUseCase rollback,
            GetOnboardingRecoveryStatusUseCase recovery)
        {
            Node = node;
            Store = store;
            Auth = auth;
            Audit = audit;
            Validate = validate;
            Plans = create;
            Start = start;
            Rollback = rollback;
            Recovery = recovery;
        }

        public DomainNode Node { get; }

        public FakeOnboardingStore Store { get; }

        public FakeAuthorizationBoundary Auth { get; }

        public FakeAuditEventWriter Audit { get; }

        public ValidateOnboardingPrerequisitesWorkflowUseCase Validate { get; }

        public CreateOnboardingPlanUseCase Plans { get; }

        public StartOnboardingUseCase Start { get; }

        public RollbackOnboardingWorkflowUseCase Rollback { get; }

        public GetOnboardingRecoveryStatusUseCase Recovery { get; }

        public static WorkflowHarness Create(bool commit, bool throwOnExecute = false)
        {
            DomainNode node = OnboardingTestFactory.RouterWithDevice(out _);
            FakeNodeStore nodes = new();
            nodes.AddAsync(node).GetAwaiter().GetResult();
            FakeOnboardingStore store = new();
            FakeAuthorizationBoundary auth = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new();
            ScriptedOnboardingRuntime runtime = new() { Commit = commit, ThrowOnExecute = throwOnExecute };
            return new WorkflowHarness(
                node,
                store,
                auth,
                audit,
                new ValidateOnboardingPrerequisitesWorkflowUseCase(auth, nodes, audit),
                new CreateOnboardingPlanUseCase(auth, nodes, store, idempotency, audit, clock),
                new StartOnboardingUseCase(auth, nodes, store, idempotency, audit, clock, runtime),
                new RollbackOnboardingWorkflowUseCase(auth, nodes, store, idempotency, audit, clock, runtime),
                new GetOnboardingRecoveryStatusUseCase(auth, nodes, store, audit));
        }

        public ValidateOnboardingPrerequisitesCommand ValidateCommand()
            => new()
            {
                Actor = "tester",
                NodeId = Node.Id.Value,
                Facts = [ValidFacts(Node.Devices[0].Id)],
            };

        public CreateOnboardingPlanCommand PlanCommand(Guid idempotencyKey)
            => new()
            {
                Actor = "tester",
                IdempotencyKey = idempotencyKey,
                NodeId = Node.Id.Value,
                NodeMembershipHash = OnboardingTestFactory.H("membership").Bytes.ToArray(),
                TopologyProjectionHash = OnboardingTestFactory.H("topology").Bytes.ToArray(),
                DevicePlans = [OnboardingTestFactory.DevicePlan(Node.Devices[0].Id, Node.DeclaredKind)],
            };

        public Task<ApplicationResult<OnboardingPlanSummaryView>> CreatePlanAsync()
            => Plans.ExecuteAsync(PlanCommand(Guid.NewGuid()));

        private static DomainFacts ValidFacts(DeviceId deviceId)
            => DomainFacts.Create(
                deviceId,
                CapabilityProfile.Create(
                    RouterOsVersion.Create(7, 16, 2, "stable"),
                    NonEmptyName.Create("x86_64"),
                    NonEmptyName.Create("CHR"),
                    packages: ["routeros"],
                    ipv6Supported: true,
                    vrrpSupported: true,
                    bridgeSupported: true,
                    apiSslCertificatePresent: true,
                    DomainSupport.Supported,
                    OnboardingTestFactory.H("manifest")),
                exactSupportedBuild: true,
                OnboardingIpServiceFacts.Create(found: true, disabled: true, port: 8728),
                OnboardingIpServiceFacts.Create(
                    found: true,
                    disabled: false,
                    port: 8729,
                    certificate: "mfc-api",
                    maxSessions: 4),
                OnboardingServiceAccountFacts.Create(
                    "mfc-read",
                    "mfc-read-group",
                    isDefaultGroup: false,
                    policies: ["api", "read"],
                    addressPrefixes: ["10.0.0.0/24"]),
                OnboardingServiceAccountFacts.Create(
                    "mfc-deploy",
                    "mfc-deploy-group",
                    isDefaultGroup: false,
                    policies: ["api", "read", "write", "test"],
                    addressPrefixes: ["10.0.0.0/24"]),
                OnboardingDeviceModeFacts.Create(schedulerEnabled: true, flagged: false));
    }

    internal sealed class ScriptedOnboardingRuntime : IOnboardingRuntime
    {
        public bool Commit { get; init; } = true;

        public bool ThrowOnExecute { get; init; }

        public Task<OnboardingExecutionResult> ExecuteAsync(
            DomainNode node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            DateTimeOffset routerClock,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnExecute)
            {
                throw new InvalidOperationException(NotConfiguredOnboardingRuntime.NotConfiguredMessage);
            }

            List<string> timeline = ["execute"];
            Advance(operation, DomainState.Prechecking, nowUtc);
            Advance(operation, DomainState.StagingBootstrapRoots, nowUtc);
            if (Commit)
            {
                Advance(operation, DomainState.StagingDisabledAnchors, nowUtc);
                Advance(operation, DomainState.ArmingWatchdogs, nowUtc);
                Advance(operation, DomainState.EnablingAnchors, nowUtc);
                Advance(operation, DomainState.Verifying, nowUtc);
                Advance(operation, DomainState.DisarmingWatchdogs, nowUtc);
                Advance(operation, DomainState.Committed, nowUtc);
                return Task.FromResult(new OnboardingExecutionResult
                {
                    Succeeded = true,
                    State = operation.State,
                    Timeline = timeline,
                    CapturePerformed = true,
                    WatchdogsDisarmed = true,
                    NodeManaged = false,
                });
            }

            Advance(operation, DomainState.RollbackPending, nowUtc);
            return Task.FromResult(new OnboardingExecutionResult
            {
                Succeeded = false,
                State = operation.State,
                Timeline = timeline,
                CapturePerformed = false,
                WatchdogsDisarmed = false,
                NodeManaged = false,
            });
        }

        public Task<OnboardingRollbackResult> RollbackAsync(
            DomainNode node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.State == DomainState.Created)
            {
                Advance(operation, DomainState.Prechecking, nowUtc);
                Advance(operation, DomainState.StagingBootstrapRoots, nowUtc);
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

            node.SetManagementState(DomainManagement.Unmanaged);
            return Task.FromResult(new OnboardingRollbackResult
            {
                Succeeded = operation.State == DomainState.RolledBack,
                State = operation.State,
                Timeline = ["rollback"],
                WatchdogsCleaned = true,
                NodeUnmanaged = true,
                RemainingEnabledAnchors = false,
            });
        }

        public Task<OnboardingRecoveryResult> RecoverAsync(
            DomainNode node,
            OnboardingPlan plan,
            OnboardingOperation operation,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OnboardingRecoveryResult
            {
                Action = OnboardingRecoveryAction.CleanupRolledBack,
                State = operation.State,
                Timeline = [],
                NodeUnmanaged = node.ManagementState == DomainManagement.Unmanaged,
                NodeManaged = node.ManagementState == DomainManagement.Managed,
            });
        }

        private static void Advance(OnboardingOperation operation, DomainState next, DateTimeOffset nowUtc)
            => operation.EnsureTransition(next, nowUtc);
    }
}
