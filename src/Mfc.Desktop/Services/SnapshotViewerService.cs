using System.Globalization;
using System.Text;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Builds snapshot viewer presentation from SnapshotService.
/// Never requests raw payloads; strips credential-like field names from display/export.
/// </summary>
public sealed class SnapshotViewerService : ISnapshotViewerService
{
    public const string UnknownPropertiesSectionId = "compatibility.unknown-properties";

    private static readonly string[] CredentialFieldTokens =
    [
        "password",
        "passwd",
        "secret",
        "cipher",
        "private-key",
        "private_key",
        "psk",
        "credential",
    ];

    private readonly ISnapshotViewerClient _client;
    private SnapshotViewerLoadResult _current = Empty();

    public SnapshotViewerService(ISnapshotViewerClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public SnapshotViewerLoadResult Current => _current;

    public void Clear() => _current = Empty();

    public async Task<SnapshotViewerLoadResult> LoadDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        _current = CloneLoading(_current, deviceId: deviceId);
        try
        {
            IReadOnlyList<SnapshotSummary> captures = await _client
                .ListCapturesAsync(deviceId, cancellationToken)
                .ConfigureAwait(false);
            List<SnapshotCaptureListItem> items = captures
                .OrderByDescending(c => c.CompletedAt?.ToDateTimeOffset() ?? DateTimeOffset.MinValue)
                .ThenByDescending(c => DesktopProtoUuid.ToGuid(c.CaptureId))
                .Select(MapCaptureListItem)
                .ToList();

            SnapshotSummary? latestCompleted = captures
                .Where(c => c.Status == SnapshotCaptureStatus.Completed)
                .OrderByDescending(c => c.CompletedAt?.ToDateTimeOffset() ?? DateTimeOffset.MinValue)
                .ThenByDescending(c => DesktopProtoUuid.ToGuid(c.CaptureId))
                .FirstOrDefault();

            if (latestCompleted is null)
            {
                _current = new SnapshotViewerLoadResult
                {
                    Succeeded = true,
                    DeviceId = deviceId,
                    Captures = items,
                    Error = items.Count == 0 ? "No captures for this device." : "No completed capture to view.",
                };
                return _current;
            }

            return await LoadCaptureCoreAsync(
                    DesktopProtoUuid.ToGuid(latestCompleted.CaptureId),
                    deviceId,
                    items,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _current = new SnapshotViewerLoadResult
            {
                Succeeded = false,
                DeviceId = deviceId,
                Error = "Load cancelled.",
                Captures = _current.Captures,
            };
            throw;
        }
        catch (Exception ex)
        {
            _current = new SnapshotViewerLoadResult
            {
                Succeeded = false,
                DeviceId = deviceId,
                Error = ex.Message,
                Captures = _current.Captures,
                Sections = _current.Sections,
            };
            return _current;
        }
    }

    public Task<SnapshotViewerLoadResult> LoadCaptureAsync(
        Guid captureId,
        CancellationToken cancellationToken = default)
        => LoadCaptureCoreAsync(captureId, deviceId: null, captures: null, cancellationToken);

    public async Task<SnapshotViewerLoadResult> LoadSectionAsync(
        Guid captureId,
        string sectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        SnapshotViewerLoadResult baseline = _current;
        _current = CloneLoading(baseline, captureId: captureId);
        try
        {
            IReadOnlyList<SnapshotRecord> configuration = await TryLoadDomainAsync(
                    captureId,
                    sectionId,
                    DiffDomain.Configuration,
                    cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<SnapshotRecord> observations = await TryLoadDomainAsync(
                    captureId,
                    sectionId,
                    DiffDomain.Observation,
                    cancellationToken)
                .ConfigureAwait(false);

            _current = new SnapshotViewerLoadResult
            {
                Succeeded = true,
                DeviceId = baseline.DeviceId,
                CaptureId = captureId,
                StatusText = baseline.StatusText,
                SchemaVersion = baseline.SchemaVersion,
                ConfigurationHashHex = baseline.ConfigurationHashHex,
                ObservationHashHex = baseline.ObservationHashHex,
                CapabilityHashHex = baseline.CapabilityHashHex,
                SnapshotHashHex = baseline.SnapshotHashHex,
                CompletedAtText = baseline.CompletedAtText,
                Captures = baseline.Captures,
                Sections = baseline.Sections,
                ConfigurationRecords = MapRecords(configuration, SnapshotRecordDomainKind.Configuration),
                ObservationRecords = MapRecords(observations, SnapshotRecordDomainKind.Observation),
            };
            return _current;
        }
        catch (OperationCanceledException)
        {
            _current = new SnapshotViewerLoadResult
            {
                Succeeded = false,
                DeviceId = baseline.DeviceId,
                CaptureId = captureId,
                Error = "Section load cancelled.",
                StatusText = baseline.StatusText,
                SchemaVersion = baseline.SchemaVersion,
                ConfigurationHashHex = baseline.ConfigurationHashHex,
                ObservationHashHex = baseline.ObservationHashHex,
                CapabilityHashHex = baseline.CapabilityHashHex,
                SnapshotHashHex = baseline.SnapshotHashHex,
                CompletedAtText = baseline.CompletedAtText,
                Captures = baseline.Captures,
                Sections = baseline.Sections,
            };
            throw;
        }
        catch (Exception ex)
        {
            _current = new SnapshotViewerLoadResult
            {
                Succeeded = false,
                DeviceId = baseline.DeviceId,
                CaptureId = captureId,
                Error = ex.Message,
                StatusText = baseline.StatusText,
                SchemaVersion = baseline.SchemaVersion,
                ConfigurationHashHex = baseline.ConfigurationHashHex,
                ObservationHashHex = baseline.ObservationHashHex,
                CapabilityHashHex = baseline.CapabilityHashHex,
                SnapshotHashHex = baseline.SnapshotHashHex,
                CompletedAtText = baseline.CompletedAtText,
                Captures = baseline.Captures,
                Sections = baseline.Sections,
            };
            return _current;
        }
    }

    /// <summary>Builds a sanitized copy/export text (no credential field values).</summary>
    public static string BuildSanitizedExport(SnapshotViewerLoadResult state, bool includeTechnical)
    {
        ArgumentNullException.ThrowIfNull(state);
        StringBuilder sb = new();
        sb.AppendLine("MTDirector snapshot (read-only, sanitized)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"capture_id: {state.CaptureId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"device_id: {state.DeviceId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"status: {state.StatusText}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"schema_version: {state.SchemaVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"configuration_hash: {state.ConfigurationHashHex}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"observation_hash: {state.ObservationHashHex}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"capability_hash: {state.CapabilityHashHex}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"completed_at: {state.CompletedAtText}");
        sb.AppendLine("sections:");
        foreach (SnapshotSectionListItem section in state.Sections)
        {
            if (section.IsTechnicalOnly && !includeTechnical)
            {
                continue;
            }

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"  - {section.SectionId} status={section.StatusText} ordered={section.Ordered}");
        }

        AppendRecords(sb, "configuration", state.ConfigurationRecords);
        AppendRecords(sb, "observations", state.ObservationRecords);
        return sb.ToString();
    }

    private async Task<SnapshotViewerLoadResult> LoadCaptureCoreAsync(
        Guid captureId,
        Guid? deviceId,
        IReadOnlyList<SnapshotCaptureListItem>? captures,
        CancellationToken cancellationToken)
    {
        _current = CloneLoading(_current, deviceId: deviceId, captureId: captureId, captures: captures);
        try
        {
            SnapshotSummary summary = await _client.GetSummaryAsync(captureId, cancellationToken)
                .ConfigureAwait(false);
            Guid resolvedDevice = deviceId ?? DesktopProtoUuid.ToGuid(summary.DeviceId);
            IReadOnlyList<SnapshotCaptureListItem> captureItems = captures ?? _current.Captures;
            List<SnapshotSectionListItem> sections = summary.Sections
                .OrderBy(s => s.SectionId, StringComparer.Ordinal)
                .Select(MapSection)
                .ToList();

            _current = new SnapshotViewerLoadResult
            {
                Succeeded = true,
                DeviceId = resolvedDevice,
                CaptureId = captureId,
                StatusText = FormatEnum(summary.Status),
                SchemaVersion = summary.SchemaVersion,
                ConfigurationHashHex = FormatHash(summary.ConfigurationHash),
                ObservationHashHex = FormatHash(summary.ObservationHash),
                CapabilityHashHex = FormatHash(summary.CapabilityHash),
                SnapshotHashHex = FormatHash(summary.SnapshotHash),
                CompletedAtText = summary.CompletedAt is null
                    ? "—"
                    : summary.CompletedAt.ToDateTimeOffset().UtcDateTime.ToString("u", CultureInfo.InvariantCulture),
                Captures = captureItems,
                Sections = sections,
            };
            return _current;
        }
        catch (OperationCanceledException)
        {
            _current = new SnapshotViewerLoadResult
            {
                Succeeded = false,
                DeviceId = deviceId ?? _current.DeviceId,
                CaptureId = captureId,
                Error = "Load cancelled.",
                Captures = captures ?? _current.Captures,
            };
            throw;
        }
        catch (Exception ex)
        {
            _current = new SnapshotViewerLoadResult
            {
                Succeeded = false,
                DeviceId = deviceId ?? _current.DeviceId,
                CaptureId = captureId,
                Error = ex.Message,
                Captures = captures ?? _current.Captures,
            };
            return _current;
        }
    }

    private async Task<IReadOnlyList<SnapshotRecord>> TryLoadDomainAsync(
        Guid captureId,
        string sectionId,
        DiffDomain domain,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client
                .GetAllSectionRecordsAsync(captureId, sectionId, domain, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("NotFound", StringComparison.Ordinal))
        {
            return [];
        }
    }

    private static SnapshotViewerLoadResult CloneLoading(
        SnapshotViewerLoadResult previous,
        Guid? deviceId = null,
        Guid? captureId = null,
        IReadOnlyList<SnapshotCaptureListItem>? captures = null)
        => new()
        {
            Succeeded = previous.Succeeded,
            Error = previous.Error,
            DeviceId = deviceId ?? previous.DeviceId,
            CaptureId = captureId ?? previous.CaptureId,
            StatusText = previous.StatusText,
            SchemaVersion = previous.SchemaVersion,
            ConfigurationHashHex = previous.ConfigurationHashHex,
            ObservationHashHex = previous.ObservationHashHex,
            CapabilityHashHex = previous.CapabilityHashHex,
            SnapshotHashHex = previous.SnapshotHashHex,
            CompletedAtText = previous.CompletedAtText,
            Captures = captures ?? previous.Captures,
            Sections = previous.Sections,
            ConfigurationRecords = previous.ConfigurationRecords,
            ObservationRecords = previous.ObservationRecords,
            IsLoading = true,
        };

    private static SnapshotViewerLoadResult Empty()
        => new() { Succeeded = false };

    private static SnapshotCaptureListItem MapCaptureListItem(SnapshotSummary summary)
        => new()
        {
            CaptureId = DesktopProtoUuid.ToGuid(summary.CaptureId),
            StatusText = FormatEnum(summary.Status),
            SchemaVersion = summary.SchemaVersion,
            CompletedAtText = summary.CompletedAt is null
                ? "—"
                : summary.CompletedAt.ToDateTimeOffset().UtcDateTime.ToString("u", CultureInfo.InvariantCulture),
        };

    private static SnapshotSectionListItem MapSection(SnapshotSectionSummary section)
        => new()
        {
            SectionId = section.SectionId,
            StatusText = FormatEnum(section.Status),
            Ordered = section.Ordered,
            ConfigurationRecordCount = (int)section.ConfigurationRecordCount,
            ObservationRecordCount = (int)section.ObservationRecordCount,
            IsTechnicalOnly = string.Equals(
                section.SectionId,
                UnknownPropertiesSectionId,
                StringComparison.Ordinal),
        };

    private static List<SnapshotRecordListItem> MapRecords(
        IReadOnlyList<SnapshotRecord> records,
        SnapshotRecordDomainKind domain)
    {
        List<SnapshotRecordListItem> items = new(records.Count);
        foreach (SnapshotRecord record in records)
        {
            IEnumerable<CanonicalField> fields = domain == SnapshotRecordDomainKind.Configuration
                ? record.Configuration
                : record.Observations;
            List<SnapshotFieldLine> lines = [];
            foreach (CanonicalField field in fields.OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (IsCredentialField(field.Name))
                {
                    continue;
                }

                lines.Add(new SnapshotFieldLine
                {
                    Name = field.Name,
                    Value = FormatCanonicalValue(field.Value),
                });
            }

            items.Add(new SnapshotRecordListItem
            {
                StableKey = record.StableKey,
                OrdinalText = record.HasOrdinal
                    ? record.Ordinal.ToString(CultureInfo.InvariantCulture)
                    : "—",
                Domain = domain,
                Fields = lines,
            });
        }

        return items;
    }

    private static void AppendRecords(
        StringBuilder sb,
        string label,
        IReadOnlyList<SnapshotRecordListItem> records)
    {
        sb.AppendLine(label + ":");
        foreach (SnapshotRecordListItem record in records)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"  [{record.OrdinalText}] {record.StableKey}");
            foreach (SnapshotFieldLine field in record.Fields)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {field.Name}={field.Value}");
            }
        }
    }

    /// <summary>True when a canonical field name looks like a credential (omit from Desktop display).</summary>
    public static bool IsCredentialFieldName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string lower = name.Trim().ToLowerInvariant();
        return CredentialFieldTokens.Any(token => lower.Contains(token, StringComparison.Ordinal));
    }

    /// <summary>Formats a wire CanonicalValue the same way the snapshot viewer does.</summary>
    public static string FormatFieldValue(CanonicalValue? value) => FormatCanonicalValue(value);

    private static bool IsCredentialField(string name) => IsCredentialFieldName(name);

    private static string FormatHash(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return "—";
        }

        return Convert.ToHexString(hash.Value.Span).ToLowerInvariant();
    }

    private static string FormatCanonicalValue(CanonicalValue? value)
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
                "[" + string.Join(", ", value.ListValue.Values.Select(FormatCanonicalValue)) + "]",
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
}
