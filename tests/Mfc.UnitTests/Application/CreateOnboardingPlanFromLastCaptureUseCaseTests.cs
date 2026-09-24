using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Onboarding;
using Mfc.Application.Topology;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Application;

/// <summary>AUDIT-GUI-02 — Controller-built onboarding from last capture + empty Validate facts.</summary>
public sealed class CreateOnboardingPlanFromLastCaptureUseCaseTests
{
    [Fact]
    public async Task EmptyValidateFactsWithoutCaptureReturnCaptureRequiredBlocker()
    {
        Harness harness = await Harness.CreateAsync(withCapture: false);
        ApplicationResult<OnboardingPrerequisiteReportView> result = await harness.Validate.ExecuteAsync(
            new ValidateOnboardingPrerequisitesCommand
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                Facts = [],
            });

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Passed);
        Assert.Contains(result.Value.Findings, f => f.Code == OnboardingCodes.CaptureRequired);
    }

    [Fact]
    public async Task EmptyValidateFactsWithCapturePassCaptureReadiness()
    {
        Harness harness = await Harness.CreateAsync(withCapture: true);
        ApplicationResult<OnboardingPrerequisiteReportView> result = await harness.Validate.ExecuteAsync(
            new ValidateOnboardingPrerequisitesCommand
            {
                Actor = "tester",
                NodeId = harness.Node.Id.Value,
                Facts = [],
            });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Passed);
        Assert.DoesNotContain(result.Value.Findings, f => f.Code == OnboardingCodes.CaptureRequired);
    }

    [Fact]
    public async Task FromLastCaptureWithoutCaptureFailsClosed()
    {
        Harness harness = await Harness.CreateAsync(withCapture: false);
        ApplicationResult<OnboardingPlanSummaryView> result = await harness.FromCapture.ExecuteAsync(
            new CreateOnboardingPlanFromLastCaptureCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = harness.Node.Id.Value,
            });

        Assert.True(result.IsFailure);
        Assert.Equal(OnboardingCodes.CaptureRequired, result.Error!.Code);
    }

    [Fact]
    public async Task FromLastCaptureBuildsPlanFromSnapshotHashes()
    {
        Harness harness = await Harness.CreateAsync(withCapture: true);
        ApplicationResult<OnboardingPlanSummaryView> result = await harness.FromCapture.ExecuteAsync(
            new CreateOnboardingPlanFromLastCaptureCommand
            {
                Actor = "tester",
                IdempotencyKey = Guid.NewGuid(),
                NodeId = harness.Node.Id.Value,
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(harness.Node.Id.Value, result.Value!.NodeId);
        Assert.NotEmpty(result.Value.PlanHash);
        Assert.NotEmpty(result.Value.Placements);
    }

    private sealed class Harness
    {
        private Harness(
            Node node,
            ValidateOnboardingPrerequisitesWorkflowUseCase validate,
            CreateOnboardingPlanFromLastCaptureUseCase fromCapture)
        {
            Node = node;
            Validate = validate;
            FromCapture = fromCapture;
        }

        public Node Node { get; }

        public ValidateOnboardingPrerequisitesWorkflowUseCase Validate { get; }

        public CreateOnboardingPlanFromLastCaptureUseCase FromCapture { get; }

        public static async Task<Harness> CreateAsync(bool withCapture)
        {
            Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
            FakeNodeStore nodes = new();
            await nodes.AddAsync(node);
            FakeSnapshotStore snapshots = new();
            FakeAuthorizationBoundary auth = new();
            FakeOnboardingStore store = new();
            FakeIdempotencyStore idempotency = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new();
            VrrpPairConsistencyLoader vrrp = new(
                new FakeDeviceStore(), snapshots, new FakeDeviceHashStateStore());

            if (withCapture)
            {
                Hash256 digest = Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes("cap-cfg")));
                StoredSnapshot stored = new()
                {
                    Metadata = SnapshotMetadata.CreateCompleted(
                        device.Id,
                        ConfigurationHash.FromDigest(digest),
                        ObservationHash.FromDigest(digest),
                        CapabilityHash.FromDigest(digest),
                        SnapshotHash.FromDigest(digest),
                        DateTimeOffset.UtcNow),
                    SchemaVersion = 1,
                };
                await snapshots.AddAsync(stored);
                snapshots.SectionsBySnapshot[stored.Metadata.Id.Value] =
                [
                    new CanonicalSection(
                        CanonicalDomain.Observations,
                        CanonicalSectionIds.SystemResource,
                        ordered: false,
                        [
                            new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["version"] = "7.16.2",
                                ["board-name"] = "CHR",
                            }),
                        ]),
                ];
                device.RecordCompletedCapture(stored.Metadata.Id.Value);
                await nodes.UpdateAsync(node);
            }

            CreateOnboardingPlanUseCase create = new(
                auth, nodes, store, idempotency, audit, clock, vrrp, new FakeUnitOfWork());
            return new Harness(
                node,
                new ValidateOnboardingPrerequisitesWorkflowUseCase(auth, nodes, audit, vrrp),
                new CreateOnboardingPlanFromLastCaptureUseCase(create, nodes, snapshots, auth));
        }
    }
}
