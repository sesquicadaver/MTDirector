using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.3-02 AC (historical ActiveStateInterval resolver).</summary>
public sealed class ActiveStateIntervalLivingSpecTests
{
    private static readonly DateTimeOffset T08 = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T12 = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T14 = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T16 = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);

    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    private static DeviceId Device => new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    [Fact]
    public void Ac1BuildsOrderedNonOverlappingIntervals()
    {
        IReadOnlyList<ActiveStateInterval> intervals = ActiveStateIntervalBuilder.BuildIntervals(
            Device,
            [
                Transition(T08, policy: Hash(1)),
                Transition(T12, policy: Hash(2), artifact: Hash(2)),
                Transition(T16, policy: Hash(3), artifact: Hash(3), configuration: Hash(3), topology: Hash(3), proven: true),
            ]);

        Assert.Equal(3, intervals.Count);
        Assert.Equal(T08, intervals[0].ValidFrom);
        Assert.Equal(T12, intervals[0].ValidUntil);
        Assert.Equal(T12, intervals[1].ValidFrom);
        Assert.Equal(T16, intervals[1].ValidUntil);
        Assert.Null(intervals[2].ValidUntil);
    }

    [Fact]
    public void Ac2ResolvesOccurredAtInsideMiddleInterval()
    {
        ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
            Query(T14),
            Snapshot(
                Transition(T08, policy: Hash(1)),
                Transition(T12, policy: Hash(2), artifact: Hash(2)),
                Transition(T16, policy: Hash(3))));

        Assert.NotNull(result.Interval);
        Assert.Equal(Hash(2), result.Interval!.PolicyHash);
        Assert.Equal(ActiveStateCertainty.Partial, result.Certainty);
        Assert.Contains(result.Findings, f => f.Code == ActiveStateIntervalCodes.Resolved);
    }

    [Fact]
    public void Ac3ValidFromIsInclusiveAtBoundary()
    {
        ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
            Query(T08),
            Snapshot(Transition(T08, policy: Hash(1)), Transition(T16, policy: Hash(2))));

        Assert.NotNull(result.Interval);
        Assert.Equal(T08, result.Interval!.ValidFrom);
        Assert.Equal(Hash(1), result.Interval.PolicyHash);
    }

    [Fact]
    public void Ac4ActiveTailIntervalCoversLaterOccurredAt()
    {
        ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
            Query(T16.AddHours(2)),
            Snapshot(Transition(T08, policy: Hash(1)), Transition(T12, policy: Hash(2))));

        Assert.NotNull(result.Interval);
        Assert.Null(result.Interval!.ValidUntil);
        Assert.Equal(Hash(2), result.Interval.PolicyHash);
    }

    [Fact]
    public void Ac5OccurredBeforeFirstTransitionFailsClosed()
    {
        ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
            Query(T08.AddMinutes(-5)),
            Snapshot(Transition(T10, policy: Hash(1))));

        Assert.Null(result.Interval);
        Assert.Equal(ActiveStateCertainty.Unknown, result.Certainty);
        Assert.Contains(
            result.Findings,
            f => f.Code == ActiveStateIntervalCodes.OccurredBeforeFirstTransition);
    }

    [Fact]
    public void Ac6ProvenCertaintyRequiresAllHashesAndKnownFlags()
    {
        ActiveStateCertainty proven = ActiveStateIntervalClassifier.Classify(
            Transition(T10, policy: Hash(1), artifact: Hash(1), configuration: Hash(1), topology: Hash(1), proven: true));
        Assert.Equal(ActiveStateCertainty.Proven, proven);
    }

    [Fact]
    public void Ac7PartialCertaintyWhenHashesIncomplete()
    {
        ActiveStateCertainty partial = ActiveStateIntervalClassifier.Classify(
            Transition(T10, policy: Hash(1), artifact: Hash(1)));
        Assert.Equal(ActiveStateCertainty.Partial, partial);
    }

    [Fact]
    public void Ac8UnknownCertaintyWhenNoHashesPresent()
    {
        ActiveStateCertainty unknown = ActiveStateIntervalClassifier.Classify(Transition(T10));
        Assert.Equal(ActiveStateCertainty.Unknown, unknown);
    }

    [Fact]
    public void Ac9ResolverIgnoresOtherDeviceTransitions()
    {
        DeviceId other = DeviceId.New();
        ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
            Query(T10),
            Snapshot(
                new ActiveStateTransitionFact
                {
                    DeviceId = other,
                    EffectiveAt = T08,
                    PolicyHash = Hash(9),
                },
                Transition(T08, policy: Hash(1))));

        Assert.NotNull(result.Interval);
        Assert.Equal(Hash(1), result.Interval!.PolicyHash);
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        ResolveActiveStateIntervalUseCase useCase = new(auth);
        ApplicationResult<ActiveStateIntervalResultView> ok = await useCase.ExecuteAsync(
            new ResolveActiveStateIntervalCommand
            {
                Actor = "analyst",
                Query = Query(T10),
                Snapshot = Snapshot(Transition(T08, policy: Hash(1))),
            });
        Assert.True(ok.IsSuccess);
        Assert.NotNull(ok.Value!.Interval);
        Assert.Equal("Partial", ok.Value.Certainty);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentContextRead);
        ApplicationResult<ActiveStateIntervalResultView> denied = await useCase.ExecuteAsync(
            new ResolveActiveStateIntervalCommand
            {
                Actor = "analyst",
                Query = Query(T10),
                Snapshot = Snapshot(Transition(T08, policy: Hash(1))),
            });
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error?.Code);
    }

    private static ActiveStateIntervalQuery Query(DateTimeOffset occurredAt) =>
        new()
        {
            DeviceId = Device,
            OccurredAt = occurredAt,
        };

    private static ActiveStateTimelineSnapshot Snapshot(params ActiveStateTransitionFact[] transitions) =>
        new() { Transitions = transitions };

    private static ActiveStateTransitionFact Transition(
        DateTimeOffset effectiveAt,
        Hash256? policy = null,
        Hash256? artifact = null,
        Hash256? configuration = null,
        Hash256? topology = null,
        bool proven = false) =>
        new()
        {
            DeviceId = Device,
            EffectiveAt = effectiveAt,
            PolicyHash = policy,
            ArtifactHash = artifact,
            ConfigurationHash = configuration,
            TopologyHash = topology,
            ActualKnown = proven,
            AnchorKnown = proven,
        };
}
