using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Drift;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Drift;

/// <summary>Extra branch coverage for M6-02 Domain/Application drift paths.</summary>
public sealed class ManagedDriftCoverageTests
{
    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    [Fact]
    public void ClassifierCoversWarningAndIgnoredKinds()
    {
        Assert.Equal(DriftSeverity.Warning, DriftClassifier.Classify(DriftFindingKind.UnmanagedPostAnchorRule));
        Assert.Equal(DriftSeverity.Ignored, DriftClassifier.Classify(DriftFindingKind.CountersChanged));
        Assert.Equal(DriftSeverity.Critical, DriftClassifier.Classify(DriftFindingKind.UnmanagedPreAnchorRule));
    }

    [Fact]
    public void DetectorWarningWithoutHashDivergence()
    {
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(
            Hash(1),
            Hash(1),
            Hash(1),
            [new DriftFinding(DriftFindingKind.UnmanagedPostAnchorRule)]);
        Assert.Equal(DriftOutcome.WarningDrift, evaluation.Outcome);
        Assert.False(evaluation.BlocksDeployment);
        Assert.False(evaluation.ConfigurationDriftPresent);
    }

    [Fact]
    public void DetectorNoDriftWhenHashesMatchAndNoFindings()
    {
        DriftEvaluation evaluation = ManagedDriftDetector.Evaluate(Hash(1), Hash(1), Hash(1), []);
        Assert.Equal(DriftOutcome.NoDrift, evaluation.Outcome);
        Assert.False(evaluation.BlocksDeployment);
    }

    [Fact]
    public void DriftFindingEquality()
    {
        DriftFinding left = new(DriftFindingKind.ManagedRuleChanged, "a");
        DriftFinding right = new(DriftFindingKind.ManagedRuleChanged, "a");
        DriftFinding other = new(DriftFindingKind.ManagedRuleChanged, "b");
        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.False(left.Equals(other));
        Assert.False(left.Equals(null));
    }

    [Fact]
    public async Task DetectFailsWhenDeviceOrHashStateMissing()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeDeviceHashStateStore hashStates = new();
        FakeDriftEventStore drift = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();
        DetectManagedDriftUseCase useCase = new(auth, devices, hashStates, drift, audit, clock, new FakeUnitOfWork());

        ApplicationResult<DriftEventView> missingDevice = await useCase.ExecuteAsync(
            new DetectManagedDriftCommand { Actor = "tester", DeviceId = Guid.NewGuid() });
        Assert.False(missingDevice.IsSuccess);
        Assert.Equal("not_found", missingDevice.Error!.Code);

        Device device = Device.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.11", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Managed,
            rowVersion: 1,
            lastCompletedCaptureId: null);
        await devices.AddAsync(device);
        ApplicationResult<DriftEventView> missingHash = await useCase.ExecuteAsync(
            new DetectManagedDriftCommand { Actor = "tester", DeviceId = device.Id.Value });
        Assert.False(missingHash.IsSuccess);
        Assert.Equal("not_found", missingHash.Error!.Code);
    }

    [Fact]
    public async Task GetAndListPermissionAndNotFoundBranches()
    {
        FakeAuthorizationBoundary denied = new();
        denied.DeniedPermissions.Add(ApplicationPermissions.InventoryRead);
        FakeDriftEventStore store = new();
        GetDriftEventUseCase get = new(denied, store);
        ApplicationResult<DriftEventView> forbidden = await get.ExecuteAsync(
            new GetDriftEventQuery { Actor = "tester", DriftEventId = Guid.NewGuid() });
        Assert.False(forbidden.IsSuccess);

        GetDriftEventUseCase missing = new(new FakeAuthorizationBoundary(), store);
        ApplicationResult<DriftEventView> notFound = await missing.ExecuteAsync(
            new GetDriftEventQuery { Actor = "tester", DriftEventId = Guid.NewGuid() });
        Assert.False(notFound.IsSuccess);
        Assert.Equal("not_found", notFound.Error!.Code);

        ListDeviceDriftEventsUseCase listDenied = new(denied, store);
        ApplicationResult<IReadOnlyList<DriftEventView>> listForbidden = await listDenied.ExecuteAsync(
            new ListDeviceDriftEventsQuery { Actor = "tester", DeviceId = Guid.NewGuid() });
        Assert.False(listForbidden.IsSuccess);
    }

    [Fact]
    public void UnknownFindingKindThrows()
    {
        Assert.Throws<DomainInvariantException>(() => DriftClassifier.Classify((DriftFindingKind)255));
    }
}
