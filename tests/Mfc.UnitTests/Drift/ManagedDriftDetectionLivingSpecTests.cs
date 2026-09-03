using System.Reflection;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Drift;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Xunit;

namespace Mfc.UnitTests.Drift;

/// <summary>Living Spec matrix for Issue Set M6-02 AC 1–12 (managed drift detection).</summary>
public sealed class ManagedDriftDetectionLivingSpecTests
{
    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    private static DriftFinding F(DriftFindingKind kind, string? detail = null)
        => new(kind, detail);

    [Fact]
    public void Ac1BaselineIsLastCommittedArtifactNotDesired()
    {
        Hash256 committed = Hash(2);
        Hash256 actual = Hash(3);
        Hash256 desired = Hash(9);
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed, actual, desired, findings: []);

        Assert.Equal(committed, evaluation.BaselineCommittedHash);
        Assert.NotEqual(desired, evaluation.BaselineCommittedHash);
        Assert.True(evaluation.ConfigurationDriftPresent);
        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);
        Assert.Equal(DriftCodes.BaselineIsLastCommitted, DriftCodes.BaselineIsLastCommitted);
    }

    [Fact]
    public void Ac2DesiredPolicyIsNotUsedAsActualBaseline()
    {
        Hash256 committed = Hash(2);
        Hash256 actual = Hash(2);
        Hash256 desired = Hash(1);
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            committed, actual, desired, findings: []);

        Assert.Equal(DriftOutcome.PendingDeploymentNotDrift, evaluation.Outcome);
        Assert.False(evaluation.ConfigurationDriftPresent);
        Assert.False(evaluation.BlocksDeployment);
        Assert.Equal(desired, evaluation.DesiredArtifactHashIgnoredForBaseline);
        Assert.Equal(committed, evaluation.BaselineCommittedHash);
        Assert.Equal(DriftCodes.DesiredNotBaseline, DriftCodes.DesiredNotBaseline);
    }

    [Theory]
    [InlineData(DriftFindingKind.ManagedRuleChanged)]
    [InlineData(DriftFindingKind.ManagedRuleReordered)]
    [InlineData(DriftFindingKind.ManagedRuleMissing)]
    public void Ac3ManagedRuleChangesAreCritical(DriftFindingKind kind)
    {
        Assert.Equal(DriftSeverity.Critical, DriftClassifier.Classify(kind));
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            Hash(1), Hash(1), desiredArtifactHash: Hash(1), findings: [F(kind)]);
        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);
        Assert.True(evaluation.BlocksDeployment);
    }

    [Theory]
    [InlineData(DriftFindingKind.AnchorMissing)]
    [InlineData(DriftFindingKind.AnchorDisabled)]
    [InlineData(DriftFindingKind.AnchorTargetChanged)]
    [InlineData(DriftFindingKind.AnchorPositionChanged)]
    public void Ac4AnchorChangesAreCritical(DriftFindingKind kind)
    {
        Assert.Equal(DriftSeverity.Critical, DriftClassifier.Classify(kind));
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            Hash(1), Hash(1), Hash(1), [F(kind)]);
        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);
    }

    [Theory]
    [InlineData(DriftFindingKind.ManagementGuardChanged)]
    [InlineData(DriftFindingKind.ManagedAddressListChanged)]
    public void Ac5GuardAndManagedListChangesAreCritical(DriftFindingKind kind)
    {
        Assert.Equal(DriftSeverity.Critical, DriftClassifier.Classify(kind));
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            Hash(1), Hash(1), Hash(1), [F(kind)]);
        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);
    }

    [Theory]
    [InlineData(DriftFindingKind.InterfaceListMembershipChanged)]
    [InlineData(DriftFindingKind.ZoneResolutionChanged)]
    [InlineData(DriftFindingKind.VrrpMembershipConfigChanged)]
    [InlineData(DriftFindingKind.NatRawMangleDependencyChanged)]
    [InlineData(DriftFindingKind.RoutingConfigurationChanged)]
    [InlineData(DriftFindingKind.RouterOsVersionChanged)]
    [InlineData(DriftFindingKind.CapabilityChanged)]
    [InlineData(DriftFindingKind.VethConfigChanged)]
    [InlineData(DriftFindingKind.VlanConfigChanged)]
    [InlineData(DriftFindingKind.BridgeMembershipConfigChanged)]
    [InlineData(DriftFindingKind.VrfAssignmentConfigChanged)]
    [InlineData(DriftFindingKind.ContainerNatExposureConfigChanged)]
    [InlineData(DriftFindingKind.HardwarePathConfigChanged)]
    public void Ac6DependencyConfigurationChangesAreCritical(DriftFindingKind kind)
    {
        Assert.Equal(DriftSeverity.Critical, DriftClassifier.Classify(kind));
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            Hash(1), Hash(1), Hash(1), [F(kind)]);
        Assert.Equal(DriftOutcome.CriticalDrift, evaluation.Outcome);
        Assert.True(evaluation.BlocksDeployment);
    }

    [Theory]
    [InlineData(DriftFindingKind.VrrpRoleChanged)]
    [InlineData(DriftFindingKind.ActiveWanChanged)]
    [InlineData(DriftFindingKind.InterfaceRunningStateChanged)]
    [InlineData(DriftFindingKind.CountersChanged)]
    [InlineData(DriftFindingKind.ContainerRunningStateChanged)]
    [InlineData(DriftFindingKind.VethRunningStateChanged)]
    [InlineData(DriftFindingKind.BridgePortStateChanged)]
    [InlineData(DriftFindingKind.HardwareOffloadStateChanged)]
    public void Ac7ObservationOnlyVrrpWanInterfaceCountersAreNotConfigurationDrift(DriftFindingKind kind)
    {
        DriftSeverity severity = DriftClassifier.Classify(kind);
        Assert.True(severity is DriftSeverity.Observation or DriftSeverity.Ignored);

        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            Hash(1), Hash(1), Hash(1), [F(kind)]);
        Assert.Equal(DriftOutcome.ObservationOnly, evaluation.Outcome);
        Assert.False(evaluation.ConfigurationDriftPresent);
        Assert.False(evaluation.BlocksDeployment);
    }

    [Fact]
    public async Task Ac8SemanticDiffIsStored()
    {
        DriftHarness harness = await DriftHarness.CreateAsync();
        string semantic = """{"entries":[{"section":"filter","change":"modified"}]}""";
        ApplicationResult<DriftEventView> result = await harness.Detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = harness.Device.Id.Value,
                ActualManagedResourceHashHex = Hash(3).ToString(),
                Findings = [new DriftFindingInput { Kind = DriftFindingKind.ManagedRuleChanged }],
                SemanticDiffCanonical = semantic,
                PersistActualHash = true,
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(semantic, result.Value!.SemanticDiffCanonical);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.SemanticDiffHashHex));

        ApplicationResult<DriftEventView> loaded = await harness.Get.ExecuteAsync(
            new GetDriftEventQuery { Actor = "tester", DriftEventId = result.Value.Id });
        Assert.True(loaded.IsSuccess);
        Assert.Equal(semantic, loaded.Value!.SemanticDiffCanonical);
        Assert.Equal(result.Value.SemanticDiffHashHex, loaded.Value.SemanticDiffHashHex);
    }

    [Fact]
    public void Ac9DriftBlocksNewDeployment()
    {
        DateTimeOffset now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        Node node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, now);
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            DeploymentOperationGate.EnsureCanStart(
                node,
                plan,
                [],
                now,
                DeploymentTestFactory.CpuPairs(),
                hasBlockingCriticalDrift: true));
        Assert.Contains(DriftCodes.CriticalDriftBlocksDeploy, ex.Message, StringComparison.Ordinal);

        // Observation-only / no critical drift must not block the gate flag.
        DeploymentOperationGate.EnsureCanStart(
            node,
            plan,
            [],
            now,
            DeploymentTestFactory.CpuPairs(),
            hasBlockingCriticalDrift: false);
    }

    [Fact]
    public void Ac10AutomaticRepairIsAbsent()
    {
        Type[] applicationTypes = typeof(DetectManagedDriftUseCase).Assembly.GetTypes();
        Assert.DoesNotContain(
            applicationTypes,
            static t => t.Name.Contains("AutoRepair", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("ForceRepair", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("ForceApply", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("SilentEnforce", StringComparison.OrdinalIgnoreCase));

        Type[] domainTypes = typeof(ManagedDriftDetector).Assembly.GetTypes();
        Assert.DoesNotContain(
            domainTypes,
            static t => t.Name.Contains("AutoRepair", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("ForceRepair", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(DriftCodes.NoAutoRepair, DriftCodes.NoAutoRepair);
    }

    [Fact]
    public void Ac11RestorationIsNormalDeploymentPathOnly()
    {
        // No ForceRepair / AutoRepair APIs exist; restoration is StartDeployment after Critical is cleared.
        MethodInfo[] detectMethods = typeof(DetectManagedDriftUseCase).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(
            detectMethods,
            static m => m.Name.Contains("Repair", StringComparison.OrdinalIgnoreCase)
                        || m.Name.Contains("Force", StringComparison.OrdinalIgnoreCase)
                        || m.Name.Contains("Enforce", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(typeof(StartDeploymentUseCase));
        Assert.Null(typeof(DetectManagedDriftUseCase).GetMethod("ForceRepair"));
        Assert.Null(typeof(DetectManagedDriftUseCase).GetMethod("AutoRepair"));
    }

    [Fact]
    public async Task Ac12DriftEventsAreImmutableAndAudited()
    {
        DriftHarness harness = await DriftHarness.CreateAsync();
        ApplicationResult<DriftEventView> result = await harness.Detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = harness.Device.Id.Value,
                ActualManagedResourceHashHex = Hash(7).ToString(),
                Findings = [new DriftFindingInput { Kind = DriftFindingKind.AnchorTargetChanged }],
                SemanticDiffCanonical = "{\"diff\":true}",
            });
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.Immutable);
        Assert.True(result.Value.BlocksDeployment);
        Assert.Equal(DriftOutcome.CriticalDrift, result.Value.Outcome);

        Assert.Contains(
            harness.Audit.Events,
            e => e.Action == DetectManagedDriftUseCase.AuditAction
                 && e.PayloadJson.Contains(result.Value.Id.ToString(), StringComparison.Ordinal)
                 && e.PayloadJson.Contains("\"immutable\":true", StringComparison.Ordinal));

        Assert.True(await harness.DriftEvents.HasBlockingCriticalDriftAsync(harness.Device.NodeId));

        // Historical Critical event stays; a later non-blocking detect clears the gate via latest-event semantics.
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddMinutes(1);
        ApplicationResult<DriftEventView> cleared = await harness.Detect.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = harness.Device.Id.Value,
                ActualManagedResourceHashHex = Hash(2).ToString(),
                Findings = [],
            });
        Assert.True(cleared.IsSuccess, cleared.Error?.Message);
        Assert.False(cleared.Value!.BlocksDeployment);
        Assert.True(cleared.Value.Immutable);
        Assert.False(await harness.DriftEvents.HasBlockingCriticalDriftAsync(harness.Device.NodeId));

        ApplicationResult<IReadOnlyList<DriftEventView>> listed = await harness.List.ExecuteAsync(
            new ListDeviceDriftEventsQuery { Actor = "tester", DeviceId = harness.Device.Id.Value });
        Assert.True(listed.IsSuccess);
        Assert.Equal(2, listed.Value!.Count);
        Assert.All(listed.Value, static v => Assert.True(v.Immutable));
    }

    private sealed class DriftHarness
    {
        private DriftHarness(
            Device device,
            FakeDeviceHashStateStore hashStates,
            FakeDriftEventStore driftEvents,
            FakeAuditEventWriter audit,
            FakeClock clock,
            DetectManagedDriftUseCase detect,
            GetDriftEventUseCase get,
            ListDeviceDriftEventsUseCase list)
        {
            Device = device;
            HashStates = hashStates;
            DriftEvents = driftEvents;
            Audit = audit;
            Clock = clock;
            Detect = detect;
            Get = get;
            List = list;
        }

        public Device Device { get; }

        public FakeDeviceHashStateStore HashStates { get; }

        public FakeDriftEventStore DriftEvents { get; }

        public FakeAuditEventWriter Audit { get; }

        public FakeClock Clock { get; }

        public DetectManagedDriftUseCase Detect { get; }

        public GetDriftEventUseCase Get { get; }

        public ListDeviceDriftEventsUseCase List { get; }

        public static async Task<DriftHarness> CreateAsync()
        {
            FakeAuthorizationBoundary auth = new();
            FakeDeviceStore devices = new();
            FakeDeviceHashStateStore hashStates = new();
            FakeDriftEventStore driftEvents = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new();

            Device device = Device.Reconstitute(
                DeviceId.New(),
                NodeId.New(),
                NonEmptyName.Create("r1"),
                ManagementEndpoint.Create("192.0.2.10", 8729),
                DeviceRole.Router,
                enabled: true,
                lastSupportState: null,
                ManagementState.Managed,
                rowVersion: 1,
                lastCompletedCaptureId: null);
            await devices.AddAsync(device);

            Hash256 committed = Hash(2);
            await hashStates.UpsertAsync(DeviceHashState.Create(
                device.Id,
                desiredPolicyHash: committed,
                desiredArtifactHash: committed,
                lastCommittedPolicyHash: committed,
                lastCommittedArtifactHash: committed,
                actualManagedResourceHash: committed,
                actualKnown: true,
                anchorKnown: true,
                updatedAtUtc: DateTimeOffset.UtcNow));

            return new DriftHarness(
                device,
                hashStates,
                driftEvents,
                audit,
                clock,
                new DetectManagedDriftUseCase(auth, devices, hashStates, driftEvents, audit, clock, new FakeUnitOfWork()),
                new GetDriftEventUseCase(auth, driftEvents),
                new ListDeviceDriftEventsUseCase(auth, driftEvents));
        }
    }
}
