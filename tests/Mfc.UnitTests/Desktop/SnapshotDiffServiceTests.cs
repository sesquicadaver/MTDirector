using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

public sealed class SnapshotDiffServiceTests
{
    [Fact]
    public async Task CompareMapsServerEntriesWithoutLocalRecompute()
    {
        Guid deviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid left = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid right = Guid.Parse("22222222-2222-2222-2222-222222222222");
        FakeClient client = new()
        {
            CapturesByDevice =
            {
                [deviceId] =
                [
                    Completed(left, deviceId, hour: 9),
                    Completed(right, deviceId, hour: 10),
                ],
            },
            Diff = new DiffPage
            {
                Identical = false,
                Warnings = { "DIFF_COMPLEXITY_LIMIT" },
                Entries =
                {
                    new DiffEntry
                    {
                        SectionId = "firewall.ipv4.filter",
                        Domain = DiffDomain.Configuration,
                        RecordKey = "fwc:rule:1",
                        BeforeOrdinal = 0,
                        AfterOrdinal = 1,
                        Confidence = MatchConfidence.ControllerId,
                        Changes = { DiffChange.Moved, DiffChange.Modified },
                        FieldDiffs =
                        {
                            new FieldDiff
                            {
                                FieldName = "action",
                                Before = new CanonicalValue { StringValue = "accept" },
                                After = new CanonicalValue { StringValue = "drop" },
                            },
                        },
                        Before = new SnapshotRecord
                        {
                            StableKey = "fwc:rule:1",
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
                                    Value = new CanonicalValue { StringValue = "lab-secret" },
                                },
                            },
                        },
                        After = new SnapshotRecord
                        {
                            StableKey = "fwc:rule:1",
                            Configuration =
                            {
                                new CanonicalField
                                {
                                    Name = "action",
                                    Value = new CanonicalValue { StringValue = "drop" },
                                },
                            },
                        },
                    },
                    new DiffEntry
                    {
                        SectionId = "firewall.ipv4.address-lists",
                        Domain = DiffDomain.Configuration,
                        RecordKey = "alist|block|203.0.113.1",
                        Changes = { DiffChange.Added },
                    },
                    new DiffEntry
                    {
                        SectionId = "ha.vrrp",
                        Domain = DiffDomain.Observation,
                        RecordKey = "vrrp|1|ether1",
                        Changes = { DiffChange.StateChanged },
                    },
                    new DiffEntry
                    {
                        SectionId = "compatibility.unknown-properties",
                        Domain = DiffDomain.Compatibility,
                        RecordKey = "unknown|menu|prop",
                        Changes = { DiffChange.Added },
                    },
                },
            },
        };

        SnapshotDiffService service = new(client);
        await service.LoadCapturesAsync(deviceId);
        SnapshotDiffLoadResult result = await service.CompareAsync(left, right);

        Assert.True(result.Succeeded);
        Assert.False(result.IsNoDifferences);
        Assert.Equal(4, result.AllEntries.Count);
        Assert.Equal(4, result.SectionGroups.Count);
        SnapshotDiffEntryItem moved = result.AllEntries[0];
        Assert.Equal("firewall.ipv4.filter", moved.SectionId);
        Assert.Contains("Moved", moved.ChangesText, StringComparison.Ordinal);
        Assert.Contains("Modified", moved.ChangesText, StringComparison.Ordinal);
        Assert.Equal("order: 0 → 1", moved.OrdinalText);
        Assert.Contains(moved.FieldLines, f => f.Summary.Contains("accept → drop", StringComparison.Ordinal));
        Assert.True(moved.HasBeforeRecord);
        Assert.True(moved.HasAfterRecord);
        Assert.Contains(moved.BeforeRecordFields, f => f.Summary == "action=accept");
        Assert.DoesNotContain(moved.BeforeRecordFields, f => f.FieldName.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(moved.AfterRecordFields, f => f.Summary == "action=drop");
        Assert.Contains(result.AllEntries, e => e.SectionId == "firewall.ipv4.address-lists");
        Assert.Contains(result.AllEntries, e => e.DomainText == "Observation" && e.ChangesText.Contains("StateChanged", StringComparison.Ordinal));
        Assert.Contains(
            result.AllEntries,
            e => e.SectionId == "compatibility.unknown-properties");
        Assert.Contains("DIFF_COMPLEXITY_LIMIT", result.Warnings);
        Assert.Equal(1, client.CompareCalls);
    }

    [Fact]
    public async Task IdenticalEmptyDiffIsNoDifferences()
    {
        Guid left = Guid.NewGuid();
        Guid right = Guid.NewGuid();
        FakeClient client = new()
        {
            Diff = new DiffPage { Identical = true },
        };
        SnapshotDiffService service = new(client);

        SnapshotDiffLoadResult result = await service.CompareAsync(left, right);

        Assert.True(result.Succeeded);
        Assert.True(result.IsNoDifferences);
        Assert.Empty(result.AllEntries);
    }

    [Fact]
    public async Task SameCaptureIdsRejected()
    {
        Guid id = Guid.NewGuid();
        SnapshotDiffService service = new(new FakeClient());
        SnapshotDiffLoadResult result = await service.CompareAsync(id, id);
        Assert.False(result.Succeeded);
        Assert.Contains("different", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TakeVisibleWarningsTruncatesPastCapAndFormatsOverflow()
    {
        string[] many = Enumerable.Range(1, 20).Select(i => "w" + i).ToArray();
        IReadOnlyList<string> visible = SnapshotDiffService.TakeVisibleWarnings(many);

        Assert.Equal(SnapshotDiffService.MaxVisibleCompareWarnings, visible.Count);
        Assert.Equal("w1", visible[0]);
        Assert.Equal("w12", visible[^1]);
        Assert.Contains("+8 more warning(s) truncated", SnapshotDiffService.FormatWarningOverflow(20), StringComparison.Ordinal);
        Assert.Empty(SnapshotDiffService.FormatWarningOverflow(3));
        Assert.Equal(3, SnapshotDiffService.TakeVisibleWarnings(["a", "b", "c"]).Count);
    }

    private static SnapshotSummary Completed(Guid captureId, Guid deviceId, int hour)
        => new()
        {
            CaptureId = ToUuid(captureId),
            DeviceId = ToUuid(deviceId),
            Status = SnapshotCaptureStatus.Completed,
            SchemaVersion = 1,
            CompletedAt = Timestamp.FromDateTimeOffset(
                new DateTimeOffset(2026, 8, 9, hour, 0, 0, TimeSpan.Zero)),
        };

    private static Uuid ToUuid(Guid id)
        => new() { Value = ByteString.CopyFrom(id.ToByteArray(bigEndian: true)) };

    private sealed class FakeClient : ISnapshotViewerClient
    {
        public Dictionary<Guid, List<SnapshotSummary>> CapturesByDevice { get; } = [];

        public DiffPage Diff { get; init; } = new() { Identical = true };

        public int CompareCalls { get; private set; }

        public Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SnapshotSummary> list = CapturesByDevice.TryGetValue(deviceId, out List<SnapshotSummary>? items)
                ? items
                : [];
            return Task.FromResult(list);
        }

        public Task<SnapshotSummary> GetSummaryAsync(Guid captureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
            Guid captureId,
            string sectionId,
            DiffDomain domain,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DiffPage> CompareSnapshotsAsync(
            Guid leftCaptureId,
            Guid rightCaptureId,
            CancellationToken cancellationToken = default)
        {
            CompareCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Diff);
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
