using System.Globalization;
using System.Text;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Maps CompareSnapshots wire response to presentation DTOs without local semantic recompute
/// and without masking unknown/unsupported properties (M1-29 AC#8/#10/#11).
/// </summary>
public sealed class SnapshotDiffService : ISnapshotDiffService
{
    private readonly ISnapshotViewerClient _client;
    private SnapshotDiffLoadResult _current = Empty();

    public SnapshotDiffService(ISnapshotViewerClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public SnapshotDiffLoadResult Current => _current;

    public void Clear() => _current = Empty();

    public async Task<SnapshotDiffLoadResult> LoadCapturesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        _current = new SnapshotDiffLoadResult
        {
            Succeeded = _current.Succeeded,
            Captures = _current.Captures,
            IsLoading = true,
        };
        try
        {
            IReadOnlyList<SnapshotSummary> captures = await _client
                .ListCapturesAsync(deviceId, cancellationToken)
                .ConfigureAwait(false);
            List<SnapshotCaptureListItem> items = captures
                .Where(c => c.Status == SnapshotCaptureStatus.Completed)
                .OrderByDescending(c => c.CompletedAt?.ToDateTimeOffset() ?? DateTimeOffset.MinValue)
                .ThenByDescending(c => DesktopProtoUuid.ToGuid(c.CaptureId))
                .Select(MapCapture)
                .ToList();

            _current = new SnapshotDiffLoadResult
            {
                Succeeded = true,
                Captures = items,
                Error = items.Count < 2
                    ? "Select a device with at least two completed captures to compare."
                    : null,
            };
            return _current;
        }
        catch (OperationCanceledException)
        {
            _current = new SnapshotDiffLoadResult
            {
                Succeeded = false,
                Error = "Load cancelled.",
                Captures = _current.Captures,
            };
            throw;
        }
        catch (Exception ex)
        {
            _current = new SnapshotDiffLoadResult
            {
                Succeeded = false,
                Error = ex.Message,
                Captures = _current.Captures,
            };
            return _current;
        }
    }

    public async Task<SnapshotDiffLoadResult> CompareAsync(
        Guid leftCaptureId,
        Guid rightCaptureId,
        CancellationToken cancellationToken = default)
    {
        if (leftCaptureId == rightCaptureId)
        {
            _current = new SnapshotDiffLoadResult
            {
                Succeeded = false,
                LeftCaptureId = leftCaptureId,
                RightCaptureId = rightCaptureId,
                Captures = _current.Captures,
                Error = "Base and target captures must be different.",
            };
            return _current;
        }

        IReadOnlyList<SnapshotCaptureListItem> captures = _current.Captures;
        _current = new SnapshotDiffLoadResult
        {
            Succeeded = false,
            LeftCaptureId = leftCaptureId,
            RightCaptureId = rightCaptureId,
            Captures = captures,
            IsLoading = true,
        };
        try
        {
            DiffPage page = await _client
                .CompareSnapshotsAsync(leftCaptureId, rightCaptureId, cancellationToken)
                .ConfigureAwait(false);

            // Preserve wire order; group without re-sorting entries across the page.
            List<SnapshotDiffEntryItem> entries = page.Entries.Select(MapEntry).ToList();
            List<SnapshotDiffSectionGroup> groups = entries
                .GroupBy(e => e.SectionId, StringComparer.Ordinal)
                .Select(g => new SnapshotDiffSectionGroup
                {
                    SectionId = g.Key,
                    EntryCount = g.Count(),
                    Entries = g.ToList(),
                })
                .ToList();

            bool identical = page.Identical || entries.Count == 0;
            _current = new SnapshotDiffLoadResult
            {
                Succeeded = true,
                LeftCaptureId = leftCaptureId,
                RightCaptureId = rightCaptureId,
                Identical = page.Identical,
                IsNoDifferences = identical,
                Warnings = page.Warnings.ToArray(),
                Captures = captures,
                SectionGroups = groups,
                AllEntries = entries,
            };
            return _current;
        }
        catch (OperationCanceledException)
        {
            _current = new SnapshotDiffLoadResult
            {
                Succeeded = false,
                LeftCaptureId = leftCaptureId,
                RightCaptureId = rightCaptureId,
                Error = "Compare cancelled.",
                Captures = captures,
            };
            throw;
        }
        catch (Exception ex)
        {
            _current = new SnapshotDiffLoadResult
            {
                Succeeded = false,
                LeftCaptureId = leftCaptureId,
                RightCaptureId = rightCaptureId,
                Error = ex.Message,
                Captures = captures,
            };
            return _current;
        }
    }

    private static SnapshotCaptureListItem MapCapture(SnapshotSummary summary)
        => new()
        {
            CaptureId = DesktopProtoUuid.ToGuid(summary.CaptureId),
            StatusText = FormatEnum(summary.Status),
            SchemaVersion = summary.SchemaVersion,
            CompletedAtText = summary.CompletedAt is null
                ? "—"
                : summary.CompletedAt.ToDateTimeOffset().UtcDateTime.ToString("u", CultureInfo.InvariantCulture),
        };

    private static SnapshotDiffEntryItem MapEntry(DiffEntry entry)
    {
        string changes = entry.Changes.Count == 0
            ? "—"
            : string.Join(", ", entry.Changes.Select(FormatEnum));
        string ordinal = FormatOrdinals(entry);
        List<SnapshotDiffFieldLine> fields = entry.FieldDiffs
            .Select(static f => new SnapshotDiffFieldLine
            {
                FieldName = f.FieldName,
                Summary = FormatFieldDiff(f),
            })
            .ToList();

        return new SnapshotDiffEntryItem
        {
            SectionId = entry.SectionId,
            DomainText = FormatEnum(entry.Domain),
            ChangesText = changes,
            RecordKey = string.IsNullOrWhiteSpace(entry.RecordKey) ? "—" : entry.RecordKey,
            OrdinalText = ordinal,
            ConfidenceText = FormatEnum(entry.Confidence),
            FieldLines = fields,
        };
    }

    private static string FormatOrdinals(DiffEntry entry)
    {
        bool hasBefore = entry.HasBeforeOrdinal;
        bool hasAfter = entry.HasAfterOrdinal;
        if (!hasBefore && !hasAfter)
        {
            return "order: —";
        }

        string before = hasBefore
            ? entry.BeforeOrdinal.ToString(CultureInfo.InvariantCulture)
            : "—";
        string after = hasAfter
            ? entry.AfterOrdinal.ToString(CultureInfo.InvariantCulture)
            : "—";
        return $"order: {before} → {after}";
    }

    private static string FormatFieldDiff(FieldDiff field)
    {
        StringBuilder sb = new();
        sb.Append(field.FieldName);
        sb.Append(": ");
        if (field.Before is not null || field.After is not null)
        {
            sb.Append(FormatValue(field.Before));
            sb.Append(" → ");
            sb.Append(FormatValue(field.After));
        }

        if (field.AddedValues.Count > 0)
        {
            sb.Append(" +[");
            sb.Append(string.Join(", ", field.AddedValues.Select(FormatValue)));
            sb.Append(']');
        }

        if (field.RemovedValues.Count > 0)
        {
            sb.Append(" -[");
            sb.Append(string.Join(", ", field.RemovedValues.Select(FormatValue)));
            sb.Append(']');
        }

        return sb.ToString();
    }

    private static string FormatValue(CanonicalValue? value)
    {
        if (value is null)
        {
            return "—";
        }

        return value.KindCase switch
        {
            CanonicalValue.KindOneofCase.StringValue => value.StringValue,
            CanonicalValue.KindOneofCase.SignedInteger =>
                value.SignedInteger.ToString(CultureInfo.InvariantCulture),
            CanonicalValue.KindOneofCase.UnsignedInteger =>
                value.UnsignedInteger.ToString(CultureInfo.InvariantCulture),
            CanonicalValue.KindOneofCase.BooleanValue => value.BooleanValue ? "true" : "false",
            CanonicalValue.KindOneofCase.BinaryValue =>
                Convert.ToHexString(value.BinaryValue.Span).ToLowerInvariant(),
            CanonicalValue.KindOneofCase.ListValue =>
                "[" + string.Join(", ", value.ListValue.Values.Select(FormatValue)) + "]",
            _ => "—",
        };
    }

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw.EndsWith("Unspecified", StringComparison.Ordinal))
        {
            return "—";
        }

        return raw;
    }

    private static SnapshotDiffLoadResult Empty()
        => new() { Succeeded = false };
}
