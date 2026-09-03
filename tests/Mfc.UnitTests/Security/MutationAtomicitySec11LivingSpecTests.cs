using System.Text.Json;
using Mfc.Application.Abstractions.Integration;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Drift;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain.Drift;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Workflow;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-11 (#391) — UoW for drift detect and response-feedback append+audit.</summary>
public sealed class MutationAtomicitySec11LivingSpecTests
{
    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    [Fact]
    public async Task Ac1DetectManagedDriftPersistsEventAndAuditInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeDeviceHashStateStore hashStates = new();
        FakeDriftEventStore drift = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();
        SpyUnitOfWork unitOfWork = new();

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
        await hashStates.UpsertAsync(
            DeviceHashState.Create(
                device.Id,
                desiredPolicyHash: Hash(1),
                desiredArtifactHash: Hash(2),
                lastCommittedPolicyHash: Hash(1),
                lastCommittedArtifactHash: Hash(2),
                actualManagedResourceHash: Hash(3),
                actualKnown: true,
                anchorKnown: true,
                clock.UtcNow));

        DetectManagedDriftUseCase useCase = new(
            auth, devices, hashStates, drift, audit, clock, unitOfWork);
        ApplicationResult<DriftEventView> result = await useCase.ExecuteAsync(
            new DetectManagedDriftCommand
            {
                Actor = "tester",
                DeviceId = device.Id.Value,
                PersistActualHash = true,
                ActualManagedResourceHashHex = Hash(4).ToString(),
                Findings =
                [
                    new DriftFindingInput
                    {
                        Kind = DriftFindingKind.UnmanagedPreAnchorRule,
                        Detail = "pre",
                    },
                ],
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.Single(await drift.ListByDeviceAsync(device.Id));
        Assert.NotEmpty(audit.Events);
    }

    [Fact]
    public async Task Ac2EmitResponseFeedbackPersistsStoreAndAuditInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeResponseFeedbackEventStore store = new();
        RecordingResponseFeedbackDeliveryPort delivery = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();
        SpyUnitOfWork unitOfWork = new();
        Guid incidentId = Guid.NewGuid();

        EmitResponseFeedbackUseCase useCase = new(auth, store, delivery, audit, clock, unitOfWork);
        ApplicationResult<ResponseFeedbackEventView> result = await useCase.ExecuteAsync(
            new EmitResponseFeedbackCommand
            {
                Actor = "tester",
                Kind = ResponseFeedbackEventKind.Planned,
                IncidentId = incidentId,
                NodeId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.Single(await store.ListByIncidentAsync(new IncidentId(incidentId)));
        Assert.Single(delivery.Delivered);
        Assert.Contains(
            audit.Events,
            e => e.PayloadJson.Contains("\"delivery\"", StringComparison.Ordinal));
        using JsonDocument doc = JsonDocument.Parse(audit.Events[0].PayloadJson);
        Assert.Equal(
            ResponseFeedbackDeliveryOutcome.Delivered.ToString(),
            doc.RootElement.GetProperty("delivery").GetString());
    }

    [Fact]
    public void Ac3SourcesUseUnitOfWorkBoundaryAndDeliveryStaysOutside()
    {
        AssertSourceUsesUnitOfWork(Path.Combine("Drift", "DriftUseCases.cs"), expectedMinOccurrences: 1);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Incident", "ResponseFeedbackUseCases.cs"),
            expectedMinOccurrences: 1);

        string feedbackPath = Path.Combine(
            FindRepoRoot(), "src", "Mfc.Application", "Incident", "ResponseFeedbackUseCases.cs");
        string feedbackSource = File.ReadAllText(feedbackPath);
        Assert.Contains("_delivery", feedbackSource, StringComparison.Ordinal);
        int deliverIdx = feedbackSource.IndexOf(".DeliverAsync", StringComparison.Ordinal);
        int uowIdx = feedbackSource.IndexOf("_unitOfWork.ExecuteAsync", StringComparison.Ordinal);
        Assert.True(deliverIdx >= 0, "delivery call missing");
        Assert.True(uowIdx >= 0, "UoW call missing");
        Assert.True(
            deliverIdx < uowIdx,
            "delivery must remain outside / before the store+audit UoW boundary");
        // Delivery must not be nested inside the UoW lambda that appends store+audit.
        int storeIdx = feedbackSource.IndexOf("_store.AppendAsync", StringComparison.Ordinal);
        Assert.True(storeIdx > uowIdx, "store append must be inside UoW after ExecuteAsync");
        Assert.True(deliverIdx < storeIdx, "delivery must stay outside store append");
    }

    [Fact]
    public void Ac4KnownLimitationsDocumentsSec11()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-11", source, StringComparison.Ordinal);
        Assert.Contains("DetectManagedDrift", source, StringComparison.Ordinal);
        Assert.Contains("EmitResponseFeedback", source, StringComparison.Ordinal);
        Assert.Contains("SEC-07", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentWorkflow", source, StringComparison.Ordinal);
        Assert.Contains("UpdateConnectionProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ResponseFeedback emit stays outside the DB boundary",
            source,
            StringComparison.Ordinal);
    }

    private static void AssertSourceUsesUnitOfWork(string relativeUnderApplication, int expectedMinOccurrences)
    {
        string path = Path.Combine(FindRepoRoot(), "src", "Mfc.Application", relativeUnderApplication);
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        int count = 0;
        int idx = 0;
        while ((idx = source.IndexOf("_unitOfWork.ExecuteAsync", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "_unitOfWork.ExecuteAsync".Length;
        }

        Assert.True(
            count >= expectedMinOccurrences,
            $"{relativeUnderApplication}: expected ≥{expectedMinOccurrences} UoW ExecuteAsync, found {count}.");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return action(cancellationToken);
        }
    }
}
