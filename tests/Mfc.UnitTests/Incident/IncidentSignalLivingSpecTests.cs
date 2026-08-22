using System.Reflection;
using Mfc.Application.Abstractions.Persistence;
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

/// <summary>Living Spec matrix for Issue Set M7.3-01 AC (IncidentSignal ingress contract).</summary>
public sealed class IncidentSignalLivingSpecTests
{
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10_01 = new(2026, 8, 22, 10, 0, 1, TimeSpan.Zero);

    [Fact]
    public void Ac1ValidMinimalSignalAccepted()
    {
        EventId eventId = EventId.New();
        IncidentSignal signal = IncidentSignal.Create(
            eventId,
            "siem-evt-42",
            T10,
            T10_01,
            IncidentSignalSourceType.Siem,
            "brute_force_login",
            IncidentSeverity.High,
            85,
            "dedup:siem:42");

        Assert.Equal(eventId, signal.EventId);
        Assert.Equal("brute_force_login", signal.Category);
        Assert.Equal(85, signal.Confidence);
        Assert.Empty(signal.Entities);
        Assert.Null(signal.RawEventRef);
    }

    [Fact]
    public void Ac2RequiredFieldsEnforced()
    {
        DomainInvariantException missingCategory = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt-1",
                T10,
                T10_01,
                IncidentSignalSourceType.Ndr,
                " ",
                IncidentSeverity.Medium,
                50,
                "dedup:1"));
        Assert.Contains(IncidentSignalCodes.MissingCategory, missingCategory.Message, StringComparison.Ordinal);

        DomainInvariantException missingDedup = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt-1",
                T10,
                T10_01,
                IncidentSignalSourceType.Ndr,
                "port_scan",
                IncidentSeverity.Medium,
                50,
                " "));
        Assert.Contains(IncidentSignalCodes.MissingDeduplicationKey, missingDedup.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3ConfidenceBoundedZeroToOneHundred()
    {
        DomainInvariantException low = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt-1",
                T10,
                T10_01,
                IncidentSignalSourceType.Ids,
                "signature_match",
                IncidentSeverity.Low,
                -1,
                "dedup:1"));
        Assert.Contains(IncidentSignalCodes.InvalidConfidence, low.Message, StringComparison.Ordinal);

        DomainInvariantException high = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt-1",
                T10,
                T10_01,
                IncidentSignalSourceType.Ids,
                "signature_match",
                IncidentSeverity.Low,
                101,
                "dedup:1"));
        Assert.Contains(IncidentSignalCodes.InvalidConfidence, high.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4FlowTupleRequiresAtLeastOneFieldAndValidPorts()
    {
        DomainInvariantException empty = Assert.Throws<DomainInvariantException>(() => FlowTuple.Create());
        Assert.Contains(IncidentSignalCodes.EmptyFlowTuple, empty.Message, StringComparison.Ordinal);

        DomainInvariantException invalidPort = Assert.Throws<DomainInvariantException>(() =>
            FlowTuple.Create(sourcePort: 0));
        Assert.Contains(IncidentSignalCodes.InvalidFlowPort, invalidPort.Message, StringComparison.Ordinal);

        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.5",
            destinationAddress: "203.0.113.10",
            destinationPort: 443,
            protocol: "tcp");
        Assert.Equal("tcp", flow.Protocol);
    }

    [Fact]
    public void Ac5EntityReferenceRequiresKindAndValue()
    {
        EntityReference entity = EntityReference.Create(EntityReferenceKind.IpAddress, "198.51.100.20");
        Assert.Equal(EntityReferenceKind.IpAddress, entity.Kind);

        DomainInvariantException missingValue = Assert.Throws<DomainInvariantException>(() =>
            EntityReference.Create(EntityReferenceKind.Hostname, " "));
        Assert.Contains(IncidentSignalCodes.MissingEntityValue, missingValue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6ForbiddenIngressFieldNamesRejected()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignalIngressGuard.RejectForbiddenIngressFieldNames(["category", "raw_syslog"]));
        Assert.Contains(IncidentSignalCodes.ForbiddenIngressField, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac7InlineRawSyslogRejectedInReferences()
    {
        DomainInvariantException rawRef = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt-1",
                T10,
                T10_01,
                IncidentSignalSourceType.Siem,
                "firewall_drop",
                IncidentSeverity.Medium,
                70,
                "dedup:1",
                rawEventRef: "<13>firewall message=drop src=10.0.0.1"));

        Assert.Contains(IncidentSignalCodes.RawSyslogRejected, rawRef.Message, StringComparison.Ordinal);

        DomainInvariantException evidence = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "evt-2",
                T10,
                T10_01,
                IncidentSignalSourceType.Monitoring,
                "link_down",
                IncidentSeverity.High,
                90,
                "dedup:2",
                evidenceRefs: ["s3://evidence/ok", "firewall message=drop src=10.0.0.2"]));
        Assert.Contains(IncidentSignalCodes.RawSyslogRejected, evidence.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac8RouterOsLogRequiresNormalizedCategory()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentSignal.Create(
                EventId.New(),
                "ros-1",
                T10,
                T10_01,
                IncidentSignalSourceType.RouterOsLog,
                "syslog",
                IncidentSeverity.Info,
                60,
                "dedup:ros:1"));
        Assert.Contains(IncidentSignalCodes.RouterOsLogRequiresCategory, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac9IngestUseCaseReturnsNormalizedViewWithoutPersistencePort()
    {
        Assembly application = typeof(IngestIncidentSignalUseCase).Assembly;
        Assert.DoesNotContain(
            application.GetTypes(),
            type => type.IsInterface
                && type.Namespace is not null
                && type.Namespace.Contains("Abstractions.Persistence", StringComparison.Ordinal)
                && (type.Name.Contains("IncidentSignal", StringComparison.Ordinal)
                    || type.Name.Contains("RawSyslog", StringComparison.Ordinal)));

        FakeAuthorizationBoundary auth = new();
        IngestIncidentSignalUseCase useCase = new(auth);
        SiteId siteId = SiteId.New();
        NodeId nodeId = NodeId.New();
        FlowTuple flow = FlowTuple.Create(
            sourceAddress: "10.0.0.8",
            destinationAddress: "198.51.100.4",
            destinationPort: 443,
            protocol: "tcp");

        ApplicationResult<IncidentSignalView> result = await useCase.ExecuteAsync(new IngestIncidentSignalCommand
        {
            Actor = "analyst-1",
            EventId = EventId.New().Value,
            SourceEventId = "ndr-9001",
            OccurredAt = T10,
            ReceivedAt = T10_01,
            SourceType = IncidentSignalSourceType.Ndr,
            Category = "c2_beacon",
            Severity = IncidentSeverity.Critical,
            Confidence = 92,
            DeduplicationKey = "dedup:ndr:9001",
            SiteId = siteId.Value,
            NodeId = nodeId.Value,
            Flow = flow,
            Entities = [EntityReference.Create(EntityReferenceKind.IpAddress, "10.0.0.8")],
            Indicators = [Indicator.Create("ja3", "abc123")],
            EvidenceRefs = ["s3://evidence/ndr-9001"],
            RawEventRef = "s3://raw-events/ndr-9001.json",
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Ndr", result.Value!.SourceType);
        Assert.Equal(siteId.Value, result.Value.SiteId);
        Assert.Equal("tcp", result.Value.Flow?.Protocol);
        Assert.Single(result.Value.Indicators);
    }

    [Fact]
    public async Task Ac10UnauthorizedIngestRejected()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentSignalIngest);
        IngestIncidentSignalUseCase useCase = new(auth);

        ApplicationResult<IncidentSignalView> result = await useCase.ExecuteAsync(new IngestIncidentSignalCommand
        {
            Actor = "analyst-2",
            EventId = EventId.New().Value,
            SourceEventId = "evt-3",
            OccurredAt = T10,
            ReceivedAt = T10_01,
            SourceType = IncidentSignalSourceType.Siem,
            Category = "malware",
            Severity = IncidentSeverity.High,
            Confidence = 80,
            DeduplicationKey = "dedup:3",
        });

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error?.Code);
    }
}
