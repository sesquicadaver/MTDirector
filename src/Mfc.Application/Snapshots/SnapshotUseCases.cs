using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Diff;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Auth = Mfc.Application.Common.AuthorizationGuard;

// CaptureSnapshotUseCase lives in CaptureSnapshotUseCase.cs (M1-23).

namespace Mfc.Application.Snapshots;

public sealed class DiscoverDeviceCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

/// <summary>
/// Read-only identity probe. Does not mutate RouterOS (Vertical Slice / AC #7).
/// Full discovery is performed by <see cref="CaptureSnapshotUseCase"/>.
/// </summary>
public sealed class DiscoverDeviceUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsReadPort _routerOs;

    public DiscoverDeviceUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        IRouterOsReadPort routerOs)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(routerOs);
        _auth = auth;
        _devices = devices;
        _profiles = profiles;
        _routerOs = routerOs;
    }

    public async Task<ApplicationResult<DeviceDiscoveryView>> ExecuteAsync(
        DiscoverDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DiscoveryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationResult<(Device Device, RouterOsReadTarget Target)> prepared =
            await PrepareTargetAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            return ApplicationResults.Fail(prepared.Error!);
        }

        (Device device, RouterOsReadTarget target) = prepared.Value!;
        RouterOsProbeResult probe;
        try
        {
            probe = await _routerOs.ProbeAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ApplicationResults.Fail(
                ApplicationError.Dependency("RouterOS probe failed (sanitized)."));
        }

        device.RecordSupportState(probe.SupportState);
        await _devices.UpdateAsync(device, cancellationToken).ConfigureAwait(false);

        return ApplicationResults.Ok(new DeviceDiscoveryView
        {
            DeviceId = device.Id.Value,
            ObservedIdentity = probe.Identity,
            SupportState = probe.SupportState,
            RouterOsMutated = false,
        });
    }

    private async Task<ApplicationResult<(Device, RouterOsReadTarget)>> PrepareTargetAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        Device? device = await _devices.GetAsync(new DeviceId(deviceId), cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device '{deviceId}' not found."));
        }

        ConnectionProfileReadModel? profile = await _profiles.GetAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed($"Connection profile for device '{deviceId}' is missing."));
        }

        RouterOsReadTarget target = new()
        {
            DeviceId = device.Id,
            Endpoint = device.ManagementEndpoint,
            SecretReference = profile.SecretReference,
            TrustMode = profile.TrustMode,
            CaProfileRef = profile.CaProfileRef,
            PinnedSpkiSha256 = profile.PinnedSpkiSha256,
        };
        return ApplicationResults.Ok((device, target));
    }
}

public sealed class GetSnapshotQuery
{
    public required string Actor { get; init; }

    public required Guid SnapshotId { get; init; }
}

public sealed class GetSnapshotUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public GetSnapshotUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<SnapshotView>> ExecuteAsync(
        GetSnapshotQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        StoredSnapshot? snapshot = await _snapshots.GetAsync(new SnapshotId(query.SnapshotId), cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Snapshot '{query.SnapshotId}' not found."));
        }

        IReadOnlyList<StoredSnapshotSectionDescriptor> sections = await _snapshots
            .ListSectionDescriptorsAsync(snapshot.Metadata.Id, cancellationToken)
            .ConfigureAwait(false);
        SnapshotView view = ViewMapper.ToView(snapshot);
        return ApplicationResults.Ok(new SnapshotView
        {
            Id = view.Id,
            DeviceId = view.DeviceId,
            Status = view.Status,
            ConfigurationHashHex = view.ConfigurationHashHex,
            ObservationHashHex = view.ObservationHashHex,
            CapabilityHashHex = view.CapabilityHashHex,
            SnapshotHashHex = view.SnapshotHashHex,
            CompletedAtUtc = view.CompletedAtUtc,
            SchemaVersion = view.SchemaVersion,
            OperationId = view.OperationId,
            Deduplicated = view.Deduplicated,
            Sections = sections
                .OrderBy(static s => s.SectionId, StringComparer.Ordinal)
                .Select(static s => new SnapshotSectionSummaryView
                {
                    SectionId = s.SectionId,
                    Status = s.Status,
                    Ordered = s.Ordered,
                    ConfigurationRecordCount = s.ConfigurationRecordCount,
                    ObservationRecordCount = s.ObservationRecordCount,
                    CapabilityRecordCount = s.CapabilityRecordCount,
                    CompatibilityRecordCount = s.CompatibilityRecordCount,
                })
                .ToArray(),
        });
    }
}

public sealed class ListSnapshotsQuery
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }

    /// <summary>Page size (1..200). Defaults to 50.</summary>
    public int Limit { get; init; } = 50;

    /// <summary>Opaque cursor from a previous page.</summary>
    public string? Cursor { get; init; }
}

public sealed class SnapshotListPageView
{
    public required IReadOnlyList<SnapshotView> Items { get; init; }

    public string? NextCursor { get; init; }
}

public sealed class ListSnapshotsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public ListSnapshotsUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<SnapshotListPageView>> ExecuteAsync(
        ListSnapshotsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        int limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        StoredSnapshotPage page = await _snapshots
            .ListByDevicePageAsync(new DeviceId(query.DeviceId), limit, query.Cursor, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok(new SnapshotListPageView
        {
            Items = page.Items.Select(static s => ViewMapper.ToView(s)).ToArray(),
            NextCursor = page.NextCursor,
        });
    }
}

public sealed class GetRawSnapshotPayloadQuery
{
    public required string Actor { get; init; }

    public required Guid SnapshotId { get; init; }
}

/// <summary>
/// Returns the sanitized raw payload for a capture. Requires <see cref="ApplicationPermissions.SnapshotRawRead"/>
/// in addition to ordinary snapshot.read (M1-23 AC#11).
/// </summary>
public sealed class GetRawSnapshotPayloadUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public GetRawSnapshotPayloadUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<StoredSnapshotPayload>> ExecuteAsync(
        GetRawSnapshotPayloadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? readError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (readError is not null)
        {
            return ApplicationResults.Fail(readError);
        }

        ApplicationError? rawError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRawRead, cancellationToken).ConfigureAwait(false);
        if (rawError is not null)
        {
            return ApplicationResults.Fail(rawError);
        }

        StoredSnapshot? snapshot = await _snapshots.GetAsync(new SnapshotId(query.SnapshotId), cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Snapshot '{query.SnapshotId}' not found."));
        }

        if (snapshot.RawPayloadHash is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed("Snapshot has no raw payload."));
        }

        StoredSnapshotPayload? payload = await _snapshots
            .GetPayloadAsync(snapshot.RawPayloadHash, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound("Raw payload was not found."));
        }

        return ApplicationResults.Ok(payload);
    }
}

public sealed class CompareSnapshotsQuery
{
    public required string Actor { get; init; }

    public required Guid LeftSnapshotId { get; init; }

    public required Guid RightSnapshotId { get; init; }
}

/// <summary>
/// Semantic section diff (M1-24) with hash-level fallback when canonical sections are unavailable.
/// </summary>
public sealed class CompareSnapshotsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public CompareSnapshotsUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<SnapshotDiffView>> ExecuteAsync(
        CompareSnapshotsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotCompare, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        StoredSnapshot? left = await _snapshots.GetAsync(new SnapshotId(query.LeftSnapshotId), cancellationToken)
            .ConfigureAwait(false);
        StoredSnapshot? right = await _snapshots.GetAsync(new SnapshotId(query.RightSnapshotId), cancellationToken)
            .ConfigureAwait(false);
        if (left is null || right is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound("One or both snapshots were not found."));
        }

        if (left.Metadata.DeviceId != right.Metadata.DeviceId)
        {
            return ApplicationResults.Fail(ApplicationError.SnapshotsFromDifferentDevices());
        }

        if (left.Metadata.Status != SnapshotStatus.Completed
            || right.Metadata.Status != SnapshotStatus.Completed
            || left.Metadata.SnapshotHash is null
            || right.Metadata.SnapshotHash is null)
        {
            return ApplicationResults.Fail(ApplicationError.SnapshotNotCompleted());
        }

        if (left.Metadata.SnapshotHash.Value.Equals(right.Metadata.SnapshotHash.Value))
        {
            return ApplicationResults.Ok(new SnapshotDiffView
            {
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = right.Metadata.Id.Value,
                Identical = true,
                ChangedFields = [],
                Entries = [],
                Warnings = [],
            });
        }

        IReadOnlyList<CanonicalSection> baseSections = await _snapshots
            .LoadCanonicalSectionsAsync(left.Metadata.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<CanonicalSection> targetSections = await _snapshots
            .LoadCanonicalSectionsAsync(right.Metadata.Id, cancellationToken)
            .ConfigureAwait(false);

        if (baseSections.Count == 0 && targetSections.Count == 0)
        {
            List<string> changed = BuildHashChangedFields(left, right);
            return ApplicationResults.Ok(new SnapshotDiffView
            {
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = right.Metadata.Id.Value,
                Identical = changed.Count == 0,
                ChangedFields = changed,
                Entries = [],
                Warnings = [],
            });
        }

        DiffDocument document = SemanticDiffEngine.Compare(baseSections, targetSections);
        return ApplicationResults.Ok(new SnapshotDiffView
        {
            LeftSnapshotId = left.Metadata.Id.Value,
            RightSnapshotId = right.Metadata.Id.Value,
            Identical = document.Identical,
            ChangedFields = document.Identical ? [] : BuildHashChangedFields(left, right),
            Entries = document.Entries.Select(ToEntryView).ToArray(),
            Warnings = document.Warnings.Select(static w => new SnapshotDiffWarningView
            {
                Code = w.Code,
                Message = w.Message,
            }).ToArray(),
        });
    }

    private static List<string> BuildHashChangedFields(StoredSnapshot left, StoredSnapshot right)
    {
        List<string> changed = [];
        if (!NullableHashEquals(left.Metadata.ConfigurationHash, right.Metadata.ConfigurationHash))
        {
            changed.Add("configuration_hash");
        }

        if (!NullableHashEquals(left.Metadata.ObservationHash, right.Metadata.ObservationHash))
        {
            changed.Add("observation_hash");
        }

        if (!NullableHashEquals(left.Metadata.CapabilityHash, right.Metadata.CapabilityHash))
        {
            changed.Add("capability_hash");
        }

        if (!NullableHashEquals(left.Metadata.SnapshotHash, right.Metadata.SnapshotHash))
        {
            changed.Add("snapshot_hash");
        }

        return changed;
    }

    private static SnapshotDiffEntryView ToEntryView(DiffEntry entry)
        => new()
        {
            SectionId = entry.SectionId,
            Domain = entry.Domain.ToString(),
            Changes = entry.Changes.Select(static c => c.ToString()).ToArray(),
            Confidence = entry.Confidence.ToString(),
            RecordKey = entry.RecordKey,
            BeforeOrdinal = entry.BeforeOrdinal,
            AfterOrdinal = entry.AfterOrdinal,
            BeforeProps = entry.BeforeProps,
            AfterProps = entry.AfterProps,
            FieldChanges = entry.FieldChanges.Select(static f => new SnapshotDiffFieldChangeView
            {
                FieldName = f.FieldName,
                Before = f.Before,
                After = f.After,
                AddedValues = f.AddedValues,
                RemovedValues = f.RemovedValues,
            }).ToArray(),
        };

    private static bool NullableHashEquals<T>(T? left, T? right)
        where T : struct, IEquatable<T>
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Value.Equals(right.Value);
    }
}

public sealed class GetSnapshotSectionQuery
{
    public required string Actor { get; init; }

    public required Guid CaptureId { get; init; }

    public required string SectionId { get; init; }

    /// <summary>Optional domain filter (configuration / observation / …).</summary>
    public DiffDomain? Domain { get; init; }

    /// <summary>Page size (1..200). Defaults to 50.</summary>
    public int Limit { get; init; } = 50;

    /// <summary>Opaque cursor from a previous page (record offset).</summary>
    public string? Cursor { get; init; }
}

/// <summary>
/// Loads canonical section records for Viewer. Never returns raw unredacted payloads (M1-26 AC#6).
/// </summary>
public sealed class GetSnapshotSectionUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISnapshotStore _snapshots;

    public GetSnapshotSectionUseCase(IAuthorizationBoundary auth, ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<SnapshotSectionPageView>> ExecuteAsync(
        GetSnapshotSectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.SnapshotRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        if (string.IsNullOrWhiteSpace(query.SectionId))
        {
            return ApplicationResults.Fail(ApplicationError.Failed("section_id is required."));
        }

        StoredSnapshot? snapshot = await _snapshots.GetAsync(new SnapshotId(query.CaptureId), cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Snapshot '{query.CaptureId}' not found."));
        }

        IReadOnlyList<CanonicalSection> sections = await _snapshots
            .LoadCanonicalSectionsAsync(snapshot.Metadata.Id, cancellationToken)
            .ConfigureAwait(false);

        string sectionId = query.SectionId.Trim();
        CanonicalSection? matched = sections.FirstOrDefault(s =>
            string.Equals(s.SectionId, sectionId, StringComparison.Ordinal)
            && (query.Domain is null || MapDomain(s) == query.Domain.Value));

        if (matched is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Section '{sectionId}' was not found for capture."));
        }

        int limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        int offset = DecodeOffset(query.Cursor);
        if (offset < 0 || offset > matched.Records.Count)
        {
            return ApplicationResults.Fail(ApplicationError.Failed("Invalid page token."));
        }

        DiffDomain domain = MapDomain(matched);
        List<SnapshotRecordView> page = [];
        int end = Math.Min(offset + limit, matched.Records.Count);
        for (int i = offset; i < end; i++)
        {
            DiffRecordView view = new(matched.Records[i], i, matched.SectionId, domain);
            Dictionary<string, string> props = new(view.Properties, StringComparer.Ordinal);
            page.Add(new SnapshotRecordView
            {
                StableKey = view.RecordKey,
                Ordinal = matched.Ordered ? view.Ordinal : null,
                Configuration = domain == DiffDomain.Configuration ? props : new Dictionary<string, string>(StringComparer.Ordinal),
                Observations = domain == DiffDomain.Observation ? props : new Dictionary<string, string>(StringComparer.Ordinal),
            });
        }

        string? next = end < matched.Records.Count ? EncodeOffset(end) : null;
        return ApplicationResults.Ok(new SnapshotSectionPageView
        {
            CaptureId = query.CaptureId,
            SectionId = matched.SectionId,
            Ordered = matched.Ordered,
            Records = page,
            NextCursor = next,
        });
    }

    private static DiffDomain MapDomain(CanonicalSection section)
    {
        if (section.SectionId.StartsWith("capability.", StringComparison.Ordinal)
            || string.Equals(section.SectionId, "capability.profile", StringComparison.Ordinal))
        {
            return DiffDomain.Capability;
        }

        if (section.SectionId.StartsWith("compatibility.", StringComparison.Ordinal))
        {
            return DiffDomain.Compatibility;
        }

        return section.Domain == CanonicalDomain.Configuration
            ? DiffDomain.Configuration
            : DiffDomain.Observation;
    }

    private static int DecodeOffset(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        return int.TryParse(cursor, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int offset)
            ? offset
            : -1;
    }

    private static string EncodeOffset(int offset)
        => offset.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
