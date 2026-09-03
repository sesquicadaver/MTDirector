using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Integration;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;
using Mfc.Domain.Incident.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.4-05 AC (RESPONSE_* feedback to external complex).</summary>
public sealed class ResponseFeedbackLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid IncidentGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid NodeGuid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(ResponseFeedbackEventKind.Planned, ResponseFeedbackEventCodes.Planned)]
    [InlineData(ResponseFeedbackEventKind.Blocked, ResponseFeedbackEventCodes.Blocked)]
    [InlineData(ResponseFeedbackEventKind.Started, ResponseFeedbackEventCodes.Started)]
    [InlineData(ResponseFeedbackEventKind.Applied, ResponseFeedbackEventCodes.Applied)]
    [InlineData(ResponseFeedbackEventKind.Verified, ResponseFeedbackEventCodes.Verified)]
    [InlineData(ResponseFeedbackEventKind.RolledBack, ResponseFeedbackEventCodes.RolledBack)]
    [InlineData(ResponseFeedbackEventKind.RecoveryRequired, ResponseFeedbackEventCodes.RecoveryRequired)]
    [InlineData(ResponseFeedbackEventKind.Expired, ResponseFeedbackEventCodes.Expired)]
    public void Ac1EventCodesMapToAllEightKinds(ResponseFeedbackEventKind kind, string code)
    {
        Assert.Equal(code, ResponseFeedbackEventCodes.ForKind(kind));
    }

    [Fact]
    public void Ac2DomainCreateRequiresConcreteCorrelationId()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ResponseFeedbackEvent.Create(
                ResponseFeedbackEventKind.Planned,
                new IncidentId(IncidentGuid),
                new NodeId(NodeGuid),
                [],
                Guid.Empty,
                T0));
    }

    [Fact]
    public async Task Ac3EmitPersistsImmutableEvent()
    {
        FeedbackHarness harness = FeedbackHarness.Create();
        ApplicationResult<ResponseFeedbackEventView> result = await harness.Emit.ExecuteAsync(SampleCommand());
        Assert.True(result.IsSuccess, result.Error?.Message);
        IReadOnlyList<ResponseFeedbackEvent> listed = await harness.Store.ListByIncidentAsync(new IncidentId(IncidentGuid));
        Assert.Single(listed);
        Assert.Equal(ResponseFeedbackEventCodes.Planned, listed[0].EventCode);
        Assert.True(listed[0].Immutable);
    }

    [Fact]
    public async Task Ac4ConfiguredDeliveryPortReceivesEvent()
    {
        FeedbackHarness harness = FeedbackHarness.Create();
        ApplicationResult<ResponseFeedbackEventView> result = await harness.Emit.ExecuteAsync(SampleCommand());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ResponseFeedbackDeliveryOutcome.Delivered, result.Value!.DeliveryOutcome);
        Assert.Single(harness.Delivery.Delivered);
    }

    [Fact]
    public async Task Ac5NotConfiguredDeliveryStillPersistsEvent()
    {
        FakeAuthorizationBoundary auth = new();
        FakeResponseFeedbackEventStore store = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = T0 };
        NotConfiguredResponseFeedbackDeliveryPort delivery = new();
        EmitResponseFeedbackUseCase emit = new(auth, store, delivery, audit, clock, new FakeUnitOfWork());
        ApplicationResult<ResponseFeedbackEventView> result = await emit.ExecuteAsync(SampleCommand());
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ResponseFeedbackDeliveryOutcome.NotConfigured, result.Value!.DeliveryOutcome);
        Assert.Single(await store.ListByIncidentAsync(new IncidentId(IncidentGuid)));
    }

    [Fact]
    public async Task Ac6ListByIncidentRequiresAuth()
    {
        FeedbackHarness harness = FeedbackHarness.Create();
        harness.Auth.DeniedPermissions.Add(ApplicationPermissions.IncidentFeedbackRead);
        ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>> result = await harness.List.ExecuteAsync(
            new ListResponseFeedbackEventsCommand
            {
                Actor = "tester",
                IncidentId = IncidentGuid,
            });
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Ac7ListByIncidentReturnsPersistedEvents()
    {
        FeedbackHarness harness = FeedbackHarness.Create();
        await harness.Emit.ExecuteAsync(SampleCommand());
        ApplicationResult<IReadOnlyList<ResponseFeedbackEventView>> listed = await harness.List.ExecuteAsync(
            new ListResponseFeedbackEventsCommand
            {
                Actor = "tester",
                IncidentId = IncidentGuid,
            });
        Assert.True(listed.IsSuccess, listed.Error?.Message);
        Assert.Single(listed.Value!);
        Assert.Equal(ResponseFeedbackEventCodes.Planned, listed.Value![0].EventCode);
    }

    [Fact]
    public async Task Ac8AssessNotEnforceableEmitsBlockedFeedback()
    {
        FakeAuthorizationBoundary auth = new();
        FakeResponseFeedbackEventStore store = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new() { UtcNow = T0 };
        EmitResponseFeedbackUseCase feedback = ResponseFeedbackTestFactory.CreateEmit(auth, store, audit, clock);
        AssessResponseIntentFeasibilityUseCase assess = new(auth, feedback);
        ApplicationResult<ResponseIntentFeasibilityView> result = await assess.ExecuteAsync(
            new AssessResponseIntentFeasibilityCommand
            {
                Actor = "tester",
                Query = new ResponseIntentFeasibilityQuery
                {
                    Intent = ResponseIntent.Create(
                        new IncidentId(IncidentGuid),
                        new NodeId(NodeGuid),
                        ResponseIntentAction.TemporaryPreStateDeny,
                        TrafficPredicate.Create(),
                        T0.AddHours(1),
                        ResponseIntentUrgency.Normal,
                        ["evt:1"],
                        "analyst",
                        Guid.NewGuid()),
                    L2BridgeVlanBypass = true,
                },
            });
        Assert.True(result.IsSuccess, result.Error?.Message);
        IReadOnlyList<ResponseFeedbackEvent> events = await store.ListByIncidentAsync(new IncidentId(IncidentGuid));
        Assert.Contains(events, e => e.EventCode == ResponseFeedbackEventCodes.Blocked);
    }

    [Fact]
    public async Task Ac9EmitAuditRecordsEventCodeAndDelivery()
    {
        FeedbackHarness harness = FeedbackHarness.Create();
        await harness.Emit.ExecuteAsync(SampleCommand());
        Assert.Contains(
            harness.Audit.Events,
            e => e.Action == EmitResponseFeedbackUseCase.Operation
                 && e.PayloadJson.Contains(ResponseFeedbackEventCodes.Planned, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac10EmitRejectsUnauthorizedActor()
    {
        FeedbackHarness harness = FeedbackHarness.Create();
        harness.Auth.DeniedPermissions.Add(ApplicationPermissions.IncidentFeedbackEmit);
        ApplicationResult<ResponseFeedbackEventView> result = await harness.Emit.ExecuteAsync(SampleCommand());
        Assert.False(result.IsSuccess);
    }

    private static EmitResponseFeedbackCommand SampleCommand()
        => new()
        {
            Actor = "tester",
            Kind = ResponseFeedbackEventKind.Planned,
            IncidentId = IncidentGuid,
            NodeId = NodeGuid,
            CorrelationId = Guid.NewGuid(),
            PlanHash = DeploymentTestFactory.H("plan").Bytes.ToArray(),
            PolicyHash = DeploymentTestFactory.H("policy").Bytes.ToArray(),
        };

    private sealed class FeedbackHarness
    {
        public FakeAuthorizationBoundary Auth { get; }

        public FakeResponseFeedbackEventStore Store { get; }

        public RecordingResponseFeedbackDeliveryPort Delivery { get; }

        public FakeAuditEventWriter Audit { get; }

        public EmitResponseFeedbackUseCase Emit { get; }

        public ListResponseFeedbackEventsUseCase List { get; }

        private FeedbackHarness(
            FakeAuthorizationBoundary auth,
            FakeResponseFeedbackEventStore store,
            RecordingResponseFeedbackDeliveryPort delivery,
            FakeAuditEventWriter audit,
            EmitResponseFeedbackUseCase emit,
            ListResponseFeedbackEventsUseCase list)
        {
            Auth = auth;
            Store = store;
            Delivery = delivery;
            Audit = audit;
            Emit = emit;
            List = list;
        }

        public static FeedbackHarness Create()
        {
            FakeAuthorizationBoundary auth = new();
            FakeResponseFeedbackEventStore store = new();
            RecordingResponseFeedbackDeliveryPort delivery = new();
            FakeAuditEventWriter audit = new();
            FakeClock clock = new() { UtcNow = T0 };
            EmitResponseFeedbackUseCase emit = ResponseFeedbackTestFactory.CreateEmit(auth, store, audit, clock, delivery);
            ListResponseFeedbackEventsUseCase list = new(auth, store);
            return new FeedbackHarness(auth, store, delivery, audit, emit, list);
        }
    }
}

/// <summary>Adapter for tests referencing not-configured delivery port from Infrastructure.</summary>
internal sealed class NotConfiguredResponseFeedbackDeliveryPort : IResponseFeedbackDeliveryPort
{
    public Task<ResponseFeedbackDeliveryResult> DeliverAsync(
        ResponseFeedbackEvent feedbackEvent,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ResponseFeedbackDeliveryResult
        {
            Outcome = ResponseFeedbackDeliveryOutcome.NotConfigured,
        });
}
