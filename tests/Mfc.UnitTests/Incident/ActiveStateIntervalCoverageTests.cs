using Mfc.Application.Incident;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.3-02 active-state interval modules.</summary>
public sealed class ActiveStateIntervalCoverageTests
{
    private static readonly DateTimeOffset T09 = new(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T11 = new(2026, 8, 22, 11, 0, 0, TimeSpan.Zero);
    private static readonly DeviceId Device = DeviceId.New();

    private static Hash256 Hash(byte seed)
        => Hash256.Create(Enumerable.Repeat(seed, 32).ToArray());

    [Fact]
    public void BuilderRejectsDuplicateTransitionInstant()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            ActiveStateIntervalBuilder.BuildIntervals(
                Device,
                [
                    new ActiveStateTransitionFact { DeviceId = Device, EffectiveAt = T09, PolicyHash = Hash(1) },
                    new ActiveStateTransitionFact { DeviceId = Device, EffectiveAt = T09, PolicyHash = Hash(2) },
                ]));
        Assert.Contains(ActiveStateIntervalCodes.DuplicateTransitionInstant, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderRejectsDeviceMismatch()
    {
        DeviceId other = DeviceId.New();
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            ActiveStateIntervalBuilder.BuildIntervals(
                Device,
                [new ActiveStateTransitionFact { DeviceId = other, EffectiveAt = T09, PolicyHash = Hash(1) }]));
        Assert.Contains(ActiveStateIntervalCodes.DeviceMismatch, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IntervalEqualityIncludesHashFields()
    {
        IReadOnlyList<ActiveStateInterval> intervals = ActiveStateIntervalBuilder.BuildIntervals(
            Device,
            [
                new ActiveStateTransitionFact
                {
                    DeviceId = Device,
                    EffectiveAt = T09,
                    PolicyHash = Hash(1),
                    ArtifactHash = Hash(2),
                    ConfigurationHash = Hash(3),
                    TopologyHash = Hash(4),
                    ActualKnown = true,
                    AnchorKnown = true,
                },
            ]);
        ActiveStateInterval left = intervals[0];
        ActiveStateInterval right = ActiveStateIntervalBuilder.BuildIntervals(
            Device,
            [
                new ActiveStateTransitionFact
                {
                    DeviceId = Device,
                    EffectiveAt = T09,
                    PolicyHash = Hash(1),
                    ArtifactHash = Hash(2),
                    ConfigurationHash = Hash(3),
                    TopologyHash = Hash(4),
                    ActualKnown = true,
                    AnchorKnown = true,
                },
            ])[0];
        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ContainsRejectsInstantBeforeValidFrom()
    {
        ActiveStateInterval interval = ActiveStateIntervalBuilder.BuildIntervals(
            Device,
            [new ActiveStateTransitionFact { DeviceId = Device, EffectiveAt = T11, PolicyHash = Hash(1) }])[0];
        Assert.False(interval.Contains(T09));
        Assert.True(interval.Contains(T11));
    }

    [Fact]
    public void ResolverReturnsNoTimelineWhenDeviceAbsent()
    {
        ActiveStateIntervalResult result = ActiveStateIntervalResolver.Resolve(
            new ActiveStateIntervalQuery { DeviceId = Device, OccurredAt = T09 },
            new ActiveStateTimelineSnapshot { Transitions = [] });
        Assert.Contains(result.Findings, f => f.Code == ActiveStateIntervalCodes.NoTimelineData);
    }

    [Fact]
    public void ClassifierDowngradesWhenAnchorUnknown()
    {
        ActiveStateCertainty certainty = ActiveStateIntervalClassifier.Classify(
            new ActiveStateTransitionFact
            {
                DeviceId = Device,
                EffectiveAt = T09,
                PolicyHash = Hash(1),
                ArtifactHash = Hash(1),
                ConfigurationHash = Hash(1),
                TopologyHash = Hash(1),
                ActualKnown = true,
                AnchorKnown = false,
            });
        Assert.Equal(ActiveStateCertainty.Partial, certainty);
    }

    [Fact]
    public void UseCaseValidatesNullQuery()
    {
        ResolveActiveStateIntervalUseCase useCase = new(new Mfc.UnitTests.Application.Fakes.FakeAuthorizationBoundary());
        Assert.Throws<ArgumentNullException>(() =>
            useCase.ExecuteAsync(
                new ResolveActiveStateIntervalCommand
                {
                    Actor = "tester",
                    Query = null!,
                    Snapshot = new ActiveStateTimelineSnapshot(),
                }).GetAwaiter().GetResult());
    }
}
