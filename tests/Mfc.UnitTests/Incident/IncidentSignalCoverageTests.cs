using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Incident;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.3-01 incident signal ingress modules.</summary>
public sealed class IncidentSignalCoverageTests
{
    private static readonly DateTimeOffset T09 = new(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T09_05 = new(2026, 8, 22, 9, 0, 5, TimeSpan.Zero);

    [Fact]
    public void CreateRejectsEmptyEventId()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                new EventId(Guid.Empty),
                "evt",
                T09,
                T09_05,
                IncidentSignalSourceType.Siem,
                "malware",
                IncidentSeverity.High,
                50,
                "dedup"));
        Assert.Contains(IncidentSignalCodes.MissingEventId, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsReceivedBeforeOccurred()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt",
                T09_05,
                T09,
                IncidentSignalSourceType.Siem,
                "malware",
                IncidentSeverity.High,
                50,
                "dedup"));
        Assert.Contains(IncidentSignalCodes.ReceivedBeforeOccurred, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsInvalidVlanId()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt",
                T09,
                T09_05,
                IncidentSignalSourceType.Siem,
                "malware",
                IncidentSeverity.High,
                50,
                "dedup",
                vlanId: 0));
        Assert.Contains(IncidentSignalCodes.InvalidVlanId, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityReferenceEqualityUsesKindAndValue()
    {
        EntityReference left = EntityReference.Create(EntityReferenceKind.Domain, "example.com");
        EntityReference right = EntityReference.Create(EntityReferenceKind.Domain, "example.com");
        EntityReference different = EntityReference.Create(EntityReferenceKind.Domain, "other.com");

        Assert.True(left.Equals(right));
        Assert.False(left.Equals(different));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void FlowTupleEqualityIncludesAllFields()
    {
        FlowTuple left = FlowTuple.Create(sourceAddress: "10.0.0.1", protocol: "udp");
        FlowTuple right = FlowTuple.Create(sourceAddress: "10.0.0.1", protocol: "udp");
        FlowTuple different = FlowTuple.Create(sourceAddress: "10.0.0.2", protocol: "udp");

        Assert.True(left.Equals(right));
        Assert.False(left.Equals(different));
    }

    [Fact]
    public void IndicatorEqualityUsesTypeAndValue()
    {
        Indicator left = Indicator.Create("sha256", "deadbeef");
        Indicator right = Indicator.Create("sha256", "deadbeef");
        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void IncidentSignalEqualityIncludesCollections()
    {
        EventId eventId = EventId.New();
        IncidentSignal left = IncidentSignal.Create(
            eventId,
            "evt",
            T09,
            T09_05,
            IncidentSignalSourceType.FlowAnalyzer,
            "exfil",
            IncidentSeverity.Medium,
            40,
            "dedup",
            entities: [EntityReference.Create(EntityReferenceKind.IpAddress, "10.1.1.1")],
            indicators: [Indicator.Create("bytes", "9001")],
            evidenceRefs: ["ref-1"]);
        IncidentSignal right = IncidentSignal.Create(
            eventId,
            "evt",
            T09,
            T09_05,
            IncidentSignalSourceType.FlowAnalyzer,
            "exfil",
            IncidentSeverity.Medium,
            40,
            "dedup",
            entities: [EntityReference.Create(EntityReferenceKind.IpAddress, "10.1.1.1")],
            indicators: [Indicator.Create("bytes", "9001")],
            evidenceRefs: ["ref-1"]);

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void CreateRejectsMultilineEvidenceRef()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt",
                T09,
                T09_05,
                IncidentSignalSourceType.Siem,
                "malware",
                IncidentSeverity.High,
                50,
                "dedup",
                evidenceRefs: ["line1\nline2"]));
        Assert.Contains(IncidentSignalCodes.RawSyslogRejected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseCaseMapsForbiddenIngressFieldsFromCommand()
    {
        FakeAuthorizationBoundary auth = new();
        IngestIncidentSignalUseCase useCase = new(auth);

        ApplicationResult<IncidentSignalView> result = await useCase.ExecuteAsync(
            new IngestIncidentSignalCommand
            {
                Actor = "analyst",
                ForbiddenIngressFieldNames = ["syslog_payload"],
                EventId = EventId.New().Value,
                SourceEventId = "evt",
                OccurredAt = T09,
                ReceivedAt = T09_05,
                SourceType = IncidentSignalSourceType.Siem,
                Category = "malware",
                Severity = IncidentSeverity.High,
                Confidence = 50,
                DeduplicationKey = "dedup",
            });

        Assert.True(result.IsFailure);
        Assert.Contains(IncidentSignalCodes.ForbiddenIngressField, result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventIdToStringUsesGuidFormat()
    {
        EventId eventId = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        Assert.Equal("11111111-2222-3333-4444-555555555555", eventId.ToString());
    }
}
