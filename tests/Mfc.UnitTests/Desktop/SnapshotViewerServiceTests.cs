using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

public sealed class SnapshotViewerServiceTests
{
    [Fact]
    public async Task LoadDeviceLoadsSummarySectionsAndSeparatesDomains()
    {
        Guid deviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid captureId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeSnapshotViewerClient client = new()
        {
            CapturesByDevice =
            {
                [deviceId] =
                [
                    new SnapshotSummary
                    {
                        CaptureId = ToUuid(captureId),
                        DeviceId = ToUuid(deviceId),
                        Status = SnapshotCaptureStatus.Completed,
                        SchemaVersion = 1,
                        ConfigurationHash = Sha("aa"),
                        ObservationHash = Sha("bb"),
                        CapabilityHash = Sha("cc"),
                        CompletedAt = Timestamp.FromDateTimeOffset(
                            new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero)),
                        Sections =
                        {
                            new SnapshotSectionSummary
                            {
                                SectionId = "firewall.ipv4.filter",
                                Status = SnapshotSectionCaptureStatus.Ok,
                                Ordered = true,
                                ConfigurationRecordCount = 1,
                            },
                            new SnapshotSectionSummary
                            {
                                SectionId = SnapshotViewerService.UnknownPropertiesSectionId,
                                Status = SnapshotSectionCaptureStatus.Ok,
                                Ordered = false,
                            },
                        },
                    },
                ],
            },
            RecordsByKey =
            {
                [$"{captureId}|firewall.ipv4.filter|{DiffDomain.Configuration}"] =
                [
                    new SnapshotRecord
                    {
                        StableKey = "fwc:rule:1",
                        Ordinal = 0,
                        Configuration =
                        {
                            new CanonicalField
                            {
                                Name = "action",
                                Value = new CanonicalValue { StringValue = "accept" },
                            },
                            new CanonicalField
                            {
                                Name = "password",
                                Value = new CanonicalValue { StringValue = "should-not-appear" },
                            },
                        },
                    },
                ],
                [$"{captureId}|firewall.ipv4.filter|{DiffDomain.Observation}"] =
                [
                    new SnapshotRecord
                    {
                        StableKey = "fwc:rule:1",
                        Observations =
                        {
                            new CanonicalField
                            {
                                Name = "bytes",
                                Value = new CanonicalValue { UnsignedInteger = 42 },
                            },
                        },
                    },
                ],
            },
        };

        SnapshotViewerService service = new(client);
        SnapshotViewerLoadResult loaded = await service.LoadDeviceAsync(deviceId);
        Assert.True(loaded.Succeeded);
        Assert.Equal(captureId, loaded.CaptureId);
        Assert.Equal(1u, loaded.SchemaVersion);
        Assert.Equal(64, loaded.ConfigurationHashHex.Length);
        Assert.Equal(64, loaded.ObservationHashHex.Length);
        Assert.Equal(64, loaded.CapabilityHashHex.Length);
        Assert.Equal(2, loaded.Sections.Count);
        Assert.Contains(loaded.Sections, s => s.SectionId == "firewall.ipv4.filter" && s.StatusText == "Ok");
        Assert.Contains(loaded.Sections, s => s.IsTechnicalOnly);

        SnapshotViewerLoadResult section = await service.LoadSectionAsync(captureId, "firewall.ipv4.filter");
        Assert.True(section.Succeeded);
        Assert.Single(section.ConfigurationRecords);
        Assert.DoesNotContain(
            section.ConfigurationRecords[0].Fields,
            f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(section.ConfigurationRecords[0].Fields, f => f.Name == "action");
        Assert.Single(section.ObservationRecords);
        Assert.Equal("42", section.ObservationRecords[0].Fields[0].Value);

        string export = SnapshotViewerService.BuildSanitizedExport(section, includeTechnical: false);
        Assert.DoesNotContain("should-not-appear", export, StringComparison.Ordinal);
        Assert.DoesNotContain(SnapshotViewerService.UnknownPropertiesSectionId, export, StringComparison.Ordinal);
        Assert.Contains("configuration_hash:", export, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadDeviceWithoutCapturesSetsFriendlyError()
    {
        Guid deviceId = Guid.NewGuid();
        FakeSnapshotViewerClient client = new();
        client.CapturesByDevice[deviceId] = [];
        SnapshotViewerService service = new(client);

        SnapshotViewerLoadResult result = await service.LoadDeviceAsync(deviceId);

        Assert.True(result.Succeeded);
        Assert.Equal("No captures for this device.", result.Error);
        Assert.Null(result.CaptureId);
    }

    [Fact]
    public async Task LoadSectionMapsAllRecordFieldsNotOnlySummaryLine()
    {
        Guid deviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid captureId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeSnapshotViewerClient client = new()
        {
            CapturesByDevice =
            {
                [deviceId] =
                [
                    new SnapshotSummary
                    {
                        CaptureId = ToUuid(captureId),
                        DeviceId = ToUuid(deviceId),
                        Status = SnapshotCaptureStatus.Completed,
                        SchemaVersion = 1,
                        Sections =
                        {
                            new SnapshotSectionSummary
                            {
                                SectionId = "firewall.ipv4.filter",
                                Status = SnapshotSectionCaptureStatus.Ok,
                                Ordered = true,
                                ConfigurationRecordCount = 1,
                            },
                        },
                    },
                ],
            },
            RecordsByKey =
            {
                [$"{captureId}|firewall.ipv4.filter|{DiffDomain.Configuration}"] =
                [
                    new SnapshotRecord
                    {
                        StableKey = "fwc:rule:day1",
                        Ordinal = 7,
                        Configuration =
                        {
                            Field("action", "drop"),
                            Field("chain", "forward"),
                            Field("comment", "day-1 lab deny guest"),
                            Field("dst-address", "10.20.0.0/16"),
                            Field("protocol", "tcp"),
                            Field("src-address", "192.168.88.0/24"),
                        },
                    },
                ],
            },
        };

        SnapshotViewerService service = new(client);
        await service.LoadDeviceAsync(deviceId);
        SnapshotViewerLoadResult section = await service.LoadSectionAsync(captureId, "firewall.ipv4.filter");

        Assert.True(section.Succeeded);
        SnapshotRecordListItem record = Assert.Single(section.ConfigurationRecords);
        Assert.Equal(6, record.Fields.Count);
        Assert.True(record.HasMoreFields);
        Assert.Contains(record.Fields, f => f.Name == "chain" && f.Value == "forward");
        Assert.Contains(record.Fields, f => f.Name == "action" && f.Value == "drop");
        Assert.Contains(record.Fields, f => f.Name == "comment" && f.Value == "day-1 lab deny guest");
        Assert.Contains(record.Fields, f => f.DisplayLine == "protocol=tcp");
        Assert.Contains(record.Fields, f => f.DisplayLine == "src-address=192.168.88.0/24");
        Assert.Contains("action=drop", record.SummaryLine, StringComparison.Ordinal);
        Assert.Contains("chain=forward", record.SummaryLine, StringComparison.Ordinal);
        Assert.DoesNotContain("src-address=", record.SummaryLine, StringComparison.Ordinal);
        Assert.DoesNotContain("protocol=", record.SummaryLine, StringComparison.Ordinal);
        Assert.EndsWith(" …", record.SummaryLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportIncludesTechnicalSectionWhenRequested()
    {
        SnapshotViewerLoadResult state = new()
        {
            Succeeded = true,
            CaptureId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            StatusText = "Completed",
            SchemaVersion = 1,
            ConfigurationHashHex = new string('a', 64),
            ObservationHashHex = new string('b', 64),
            CapabilityHashHex = new string('c', 64),
            Sections =
            [
                new SnapshotSectionListItem
                {
                    SectionId = SnapshotViewerService.UnknownPropertiesSectionId,
                    StatusText = "Ok",
                    Ordered = false,
                    ConfigurationRecordCount = 0,
                    ObservationRecordCount = 1,
                    IsTechnicalOnly = true,
                },
            ],
        };

        string withTech = SnapshotViewerService.BuildSanitizedExport(state, includeTechnical: true);
        string without = SnapshotViewerService.BuildSanitizedExport(state, includeTechnical: false);
        Assert.Contains(SnapshotViewerService.UnknownPropertiesSectionId, withTech, StringComparison.Ordinal);
        Assert.DoesNotContain(SnapshotViewerService.UnknownPropertiesSectionId, without, StringComparison.Ordinal);
    }

    private static CanonicalField Field(string name, string value)
        => new()
        {
            Name = name,
            Value = new CanonicalValue { StringValue = value },
        };

    private static Uuid ToUuid(Guid id)
        => new() { Value = ByteString.CopyFrom(id.ToByteArray(bigEndian: true)) };

    private static Sha256 Sha(string seed)
    {
        byte[] bytes = new byte[32];
        byte fill = (byte)seed[0];
        Array.Fill(bytes, fill);
        return new Sha256 { Value = ByteString.CopyFrom(bytes) };
    }

    private sealed class FakeSnapshotViewerClient : ISnapshotViewerClient
    {
        public Dictionary<Guid, List<SnapshotSummary>> CapturesByDevice { get; } = [];

        public Dictionary<string, List<SnapshotRecord>> RecordsByKey { get; } = new(StringComparer.Ordinal);


        public Task<StartCaptureResponse> StartNodeCaptureAsync(
            Guid nodeId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<SnapshotSummary> list = CapturesByDevice.TryGetValue(deviceId, out List<SnapshotSummary>? items)
                ? items
                : [];
            return Task.FromResult(list);
        }

        public Task<SnapshotSummary> GetSummaryAsync(Guid captureId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (List<SnapshotSummary> list in CapturesByDevice.Values)
            {
                SnapshotSummary? match = list.FirstOrDefault(c =>
                    c.CaptureId is not null
                    && new Guid(c.CaptureId.Value.Span, bigEndian: true) == captureId);
                if (match is not null)
                {
                    return Task.FromResult(match);
                }
            }

            throw new InvalidOperationException($"capture {captureId} not found");
        }

        public Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
            Guid captureId,
            string sectionId,
            DiffDomain domain,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = $"{captureId}|{sectionId}|{domain}";
            if (RecordsByKey.TryGetValue(key, out List<SnapshotRecord>? records))
            {
                return Task.FromResult<IReadOnlyList<SnapshotRecord>>(records);
            }

            throw new InvalidOperationException($"Section '{sectionId}' was not found for capture.");
        }

        public Task<DiffPage> CompareSnapshotsAsync(
            Guid leftCaptureId,
            Guid rightCaptureId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DiffPage { Identical = true });
        }

        public Task<StartCaptureResponse> StartCaptureAsync(
            Guid deviceId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
