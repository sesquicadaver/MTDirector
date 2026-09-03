using System.Buffers.Binary;
using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mfc.Infrastructure.Persistence.Snapshots;

/// <summary>
/// PostgreSQL-backed <see cref="ISnapshotStore"/> with atomic completed-capture persistence (M1-23).
/// </summary>
public sealed class EfSnapshotStore : ISnapshotStore
{
    /// <summary>CaptureOperation COMPLETED (Vertical Slice §7.1 order: QUEUED, RUNNING, COMPLETED, …).</summary>
    public const short CaptureOperationCompletedStatus = 2;

    /// <summary>Device-scoped capture operation target.</summary>
    public const short CaptureTargetTypeDevice = 1;

    private readonly MfcDbContext _db;

    public EfSnapshotStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<StoredSnapshot?> GetAsync(SnapshotId id, CancellationToken cancellationToken = default)
    {
        SnapshotCaptureEntity? entity = await _db.SnapshotCaptures
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        return await ToStoredAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredSnapshot>> ListByDeviceAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        List<SnapshotCaptureEntity> rows = await _db.SnapshotCaptures
            .AsNoTracking()
            .Where(c => c.DeviceId == deviceId.Value)
            .OrderByDescending(c => c.CaptureCompletedAtUtc)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StoredSnapshot> result = new(rows.Count);
        foreach (SnapshotCaptureEntity row in rows)
        {
            StoredSnapshot? mapped = await ToStoredAsync(row, cancellationToken).ConfigureAwait(false);
            if (mapped is not null)
            {
                result.Add(mapped);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<StoredSnapshotPage> ListByDevicePageAsync(
        DeviceId deviceId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Page limit must be positive.");
        }

        IQueryable<SnapshotCaptureEntity> query = _db.SnapshotCaptures
            .AsNoTracking()
            .Where(c =>
                c.DeviceId == deviceId.Value
                && c.Status == SnapshotCaptureEntity.CompletedStatus
                && c.CaptureCompletedAtUtc != null);

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            (DateTimeOffset completedAt, Guid id) = DecodeCursor(cursor);
            query = query.Where(c =>
                c.CaptureCompletedAtUtc!.Value < completedAt
                || (c.CaptureCompletedAtUtc!.Value == completedAt && c.Id.CompareTo(id) < 0));
        }

        List<SnapshotCaptureEntity> pagePlus = await query
            .OrderByDescending(c => c.CaptureCompletedAtUtc)
            .ThenByDescending(c => c.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = pagePlus.Count > limit;
        IEnumerable<SnapshotCaptureEntity> page = hasMore ? pagePlus.Take(limit) : pagePlus;

        List<StoredSnapshot> items = [];
        foreach (SnapshotCaptureEntity row in page)
        {
            StoredSnapshot? mapped = await ToStoredAsync(row, cancellationToken).ConfigureAwait(false);
            if (mapped is not null)
            {
                items.Add(mapped);
            }
        }

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            StoredSnapshot last = items[^1];
            DateTimeOffset at = last.Metadata.CompletedAtUtc
                ?? throw new InvalidOperationException("Completed snapshot page item missing CompletedAtUtc.");
            nextCursor = EncodeCursor(at, last.Metadata.Id.Value);
        }

        return new StoredSnapshotPage
        {
            Items = items,
            NextCursor = nextCursor,
        };
    }

    /// <inheritdoc />
    public async Task<StoredSnapshot?> FindCompletedBySnapshotHashAsync(
        DeviceId deviceId,
        SnapshotHash snapshotHash,
        CancellationToken cancellationToken = default)
    {
        byte[] digest = snapshotHash.Value.Bytes.ToArray();
        SnapshotCaptureEntity? entity = await _db.SnapshotCaptures
            .AsNoTracking()
            .Where(c =>
                c.DeviceId == deviceId.Value
                && c.Status == SnapshotCaptureEntity.CompletedStatus
                && c.SnapshotHash != null
                && c.SnapshotHash == digest)
            .OrderByDescending(c => c.CaptureCompletedAtUtc)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        return await ToStoredAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<StoredSnapshot?> FindByIdempotencyAsync(
        Guid requestedBy,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        CaptureOperationEntity? operation = await _db.CaptureOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.RequestedBy == requestedBy && o.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (operation is null)
        {
            return null;
        }

        SnapshotCaptureEntity? capture = await _db.SnapshotCaptures
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.OperationId == operation.Id && c.Status == SnapshotCaptureEntity.CompletedStatus,
                cancellationToken)
            .ConfigureAwait(false);
        if (capture is null)
        {
            return null;
        }

        return await ToStoredAsync(capture, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<StoredSnapshot> PersistCompletedAsync(
        SnapshotPersistRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Capture);

        DateTimeOffset completedAt = new(request.CapturedAtUtc.UtcDateTime, TimeSpan.Zero);
        Guid operationId = Guid.NewGuid();
        Guid captureId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] rawPayloadHash;
        byte[] configurationPayloadHash;
        byte[] observationPayloadHash;
        byte[] capabilityPayloadHash;

        DeviceEntity device = await _db.Devices
            .SingleOrDefaultAsync(d => d.Id == request.DeviceId.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Device '{request.DeviceId.Value}' was not found; cannot persist snapshot capture.");

        IDbContextTransaction? ambient = _db.Database.CurrentTransaction;
        bool ownsTransaction = ambient is null;
        IDbContextTransaction tx = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : ambient!;
        try
        {
            _db.CaptureOperations.Add(new CaptureOperationEntity
            {
                Id = operationId,
                TargetType = CaptureTargetTypeDevice,
                TargetId = request.DeviceId.Value,
                RequestedBy = request.RequestedBy,
                IdempotencyKey = request.IdempotencyKey,
                Status = CaptureOperationCompletedStatus,
                StartedAtUtc = completedAt,
                CompletedAtUtc = completedAt,
                CreatedAtUtc = now,
            });

            rawPayloadHash = await UpsertPayloadAsync(
                request.Capture.RawPayload,
                SnapshotPayloadKind.RawSanitized,
                request.Capture.SchemaVersion,
                cancellationToken).ConfigureAwait(false);
            configurationPayloadHash = await UpsertPayloadAsync(
                request.Capture.ConfigurationPayload,
                SnapshotPayloadKind.CanonicalConfiguration,
                request.Capture.SchemaVersion,
                cancellationToken).ConfigureAwait(false);
            observationPayloadHash = await UpsertPayloadAsync(
                request.Capture.ObservationPayload,
                SnapshotPayloadKind.CanonicalObservations,
                request.Capture.SchemaVersion,
                cancellationToken).ConfigureAwait(false);
            capabilityPayloadHash = await UpsertPayloadAsync(
                request.Capture.CapabilityPayload,
                SnapshotPayloadKind.CanonicalCapabilities,
                request.Capture.SchemaVersion,
                cancellationToken).ConfigureAwait(false);

            byte[] configurationHash = request.Capture.ConfigurationHash.Value.Bytes.ToArray();
            byte[] observationHash = request.Capture.ObservationHash.Value.Bytes.ToArray();
            byte[] capabilityHash = request.Capture.CapabilityHash.Value.Bytes.ToArray();
            byte[] snapshotHash = request.Capture.SnapshotHash.Value.Bytes.ToArray();

            string sectionResultsJson = SerializeSectionResults(request.Capture.Sections);

            _db.SnapshotCaptures.Add(new SnapshotCaptureEntity
            {
                Id = captureId,
                OperationId = operationId,
                DeviceId = request.DeviceId.Value,
                Status = SnapshotCaptureEntity.CompletedStatus,
                AttemptCount = 1,
                CaptureStartedAtUtc = completedAt,
                Pass1CompletedAtUtc = completedAt,
                Pass2CompletedAtUtc = completedAt,
                CaptureCompletedAtUtc = completedAt,
                RawPayloadHash = rawPayloadHash,
                ConfigurationPayloadHash = configurationPayloadHash,
                ObservationPayloadHash = observationPayloadHash,
                CapabilityPayloadHash = capabilityPayloadHash,
                ConfigurationHash = configurationHash,
                ObservationHash = observationHash,
                CapabilityHash = capabilityHash,
                SnapshotHash = snapshotHash,
                SectionResultsJson = sectionResultsJson,
            });

            foreach (CapturedSectionDescriptor section in request.Capture.Sections)
            {
                ArgumentNullException.ThrowIfNull(section);
                if (string.IsNullOrWhiteSpace(section.SectionId))
                {
                    throw new ArgumentException("SectionId must be non-empty.");
                }

                _db.SnapshotCaptureSections.Add(new SnapshotCaptureSectionEntity
                {
                    CaptureId = captureId,
                    SectionId = section.SectionId.Trim(),
                    SectionVersion = section.SectionVersion,
                    Status = section.Status,
                    Ordered = section.Ordered,
                    ConfigurationRecordCount = section.ConfigurationRecordCount,
                    ObservationRecordCount = section.ObservationRecordCount,
                    CapabilityRecordCount = 0,
                    CompatibilityRecordCount = 0,
                    RawHash = await OptionalPayloadAsync(
                        section.RawPayload,
                        SnapshotPayloadKind.RawSanitized,
                        request.Capture.SchemaVersion,
                        cancellationToken).ConfigureAwait(false),
                    ConfigurationHash = await OptionalPayloadAsync(
                        section.ConfigurationPayload,
                        SnapshotPayloadKind.CanonicalConfiguration,
                        request.Capture.SchemaVersion,
                        cancellationToken).ConfigureAwait(false),
                    ObservationHash = await OptionalPayloadAsync(
                        section.ObservationPayload,
                        SnapshotPayloadKind.CanonicalObservations,
                        request.Capture.SchemaVersion,
                        cancellationToken).ConfigureAwait(false),
                });
            }

            device.LastCompletedCaptureId = captureId;
            device.UpdatedAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (ownsTransaction)
            {
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            _db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                await tx.DisposeAsync().ConfigureAwait(false);
            }
        }

        SnapshotMetadata metadata = SnapshotMetadata.CreateCompleted(
            new SnapshotId(captureId),
            request.DeviceId,
            request.Capture.ConfigurationHash,
            request.Capture.ObservationHash,
            request.Capture.CapabilityHash,
            request.Capture.SnapshotHash,
            completedAt);

        return new StoredSnapshot
        {
            Metadata = metadata,
            SchemaVersion = request.Capture.SchemaVersion,
            OperationId = operationId,
            RawPayloadHash = Hash256.Create(rawPayloadHash),
            ConfigurationPayloadHash = Hash256.Create(configurationPayloadHash),
            ObservationPayloadHash = Hash256.Create(observationPayloadHash),
            CapabilityPayloadHash = Hash256.Create(capabilityPayloadHash),
        };
    }

    /// <inheritdoc />
    public async Task<StoredSnapshotPayload?> GetPayloadAsync(
        Hash256 payloadHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payloadHash);
        byte[] key = payloadHash.Bytes.ToArray();
        SnapshotPayloadEntity? entity = await _db.SnapshotPayloads
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PayloadHash == key, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        byte[] uncompressed = BrotliPayloadCodec.DecodeAndVerify(
            entity.CompressedPayload,
            (SnapshotCompression)entity.Compression,
            entity.UncompressedSize,
            entity.PayloadHash);

        return new StoredSnapshotPayload
        {
            PayloadHash = Hash256.Create(entity.PayloadHash),
            Kind = (SnapshotPayloadKind)entity.PayloadKind,
            SchemaVersion = entity.SchemaVersion,
            Compression = (SnapshotCompression)entity.Compression,
            UncompressedBytes = uncompressed,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CanonicalSection>> LoadCanonicalSectionsAsync(
        SnapshotId id,
        CancellationToken cancellationToken = default)
    {
        List<SnapshotCaptureSectionEntity> rows = await _db.SnapshotCaptureSections
            .AsNoTracking()
            .Where(s => s.CaptureId == id.Value)
            .OrderBy(s => s.SectionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CanonicalSection> sections = [];
        foreach (SnapshotCaptureSectionEntity row in rows)
        {
            await TryAddParsedSectionAsync(sections, row.ConfigurationHash, cancellationToken)
                .ConfigureAwait(false);
            await TryAddParsedSectionAsync(sections, row.ObservationHash, cancellationToken)
                .ConfigureAwait(false);
        }

        return sections;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredSnapshotSectionDescriptor>> ListSectionDescriptorsAsync(
        SnapshotId id,
        CancellationToken cancellationToken = default)
    {
        List<SnapshotCaptureSectionEntity> rows = await _db.SnapshotCaptureSections
            .AsNoTracking()
            .Where(s => s.CaptureId == id.Value)
            .OrderBy(s => s.SectionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(static row => new StoredSnapshotSectionDescriptor
        {
            SectionId = row.SectionId,
            Status = row.Status,
            Ordered = row.Ordered,
            ConfigurationRecordCount = row.ConfigurationRecordCount,
            ObservationRecordCount = row.ObservationRecordCount,
            CapabilityRecordCount = row.CapabilityRecordCount,
            CompatibilityRecordCount = row.CompatibilityRecordCount,
        }).ToArray();
    }

    private async Task TryAddParsedSectionAsync(
        List<CanonicalSection> sections,
        byte[]? payloadHash,
        CancellationToken cancellationToken)
    {
        if (payloadHash is null || payloadHash.Length == 0)
        {
            return;
        }

        StoredSnapshotPayload? payload = await GetPayloadAsync(Hash256.Create(payloadHash), cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return;
        }

        if (CanonicalSection.TryParse(payload.UncompressedBytes.Span, out CanonicalSection? section)
            && section is not null)
        {
            sections.Add(section);
        }
    }

    private async Task<byte[]> UpsertPayloadAsync(
        ReadOnlyMemory<byte> uncompressed,
        SnapshotPayloadKind kind,
        int schemaVersion,
        CancellationToken cancellationToken)
    {
        BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(uncompressed);
        SnapshotPayloadEntity? existing = await _db.SnapshotPayloads
            .FindAsync([encoded.PayloadHash], cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.SchemaVersion != schemaVersion
                || existing.UncompressedSize != encoded.UncompressedSize)
            {
                throw new InvalidOperationException(
                    "Content-addressed payload conflict: existing row has a different schema version or uncompressed size.");
            }

            return existing.PayloadHash;
        }

        _db.SnapshotPayloads.Add(new SnapshotPayloadEntity
        {
            PayloadHash = encoded.PayloadHash,
            PayloadKind = (short)kind,
            SchemaVersion = schemaVersion,
            Compression = (short)encoded.Compression,
            UncompressedSize = encoded.UncompressedSize,
            CompressedPayload = encoded.CompressedPayload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        return encoded.PayloadHash;
    }

    private async Task<byte[]?> OptionalPayloadAsync(
        ReadOnlyMemory<byte>? uncompressed,
        SnapshotPayloadKind kind,
        int schemaVersion,
        CancellationToken cancellationToken)
    {
        if (uncompressed is null || uncompressed.Value.Length == 0)
        {
            return null;
        }

        return await UpsertPayloadAsync(uncompressed.Value, kind, schemaVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<StoredSnapshot?> ToStoredAsync(
        SnapshotCaptureEntity entity,
        CancellationToken cancellationToken)
    {
        if (entity.Status != SnapshotCaptureEntity.CompletedStatus
            || entity.CaptureCompletedAtUtc is null
            || entity.ConfigurationHash is null
            || entity.ObservationHash is null
            || entity.CapabilityHash is null
            || entity.SnapshotHash is null)
        {
            // Non-completed or incomplete rows are not projected as StoredSnapshot metadata.
            if (entity.Status != SnapshotCaptureEntity.CompletedStatus)
            {
                return null;
            }

            throw new InvalidOperationException(
                $"Completed snapshot_captures row '{entity.Id}' is missing required hash columns.");
        }

        int schemaVersion = await ResolveSchemaVersionAsync(entity, cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedAt = new(entity.CaptureCompletedAtUtc.Value.UtcDateTime, TimeSpan.Zero);

        SnapshotMetadata metadata = SnapshotMetadata.CreateCompleted(
            new SnapshotId(entity.Id),
            new DeviceId(entity.DeviceId),
            ConfigurationHash.FromBytes(entity.ConfigurationHash),
            ObservationHash.FromBytes(entity.ObservationHash),
            CapabilityHash.FromBytes(entity.CapabilityHash),
            SnapshotHash.FromBytes(entity.SnapshotHash),
            completedAt);

        return new StoredSnapshot
        {
            Metadata = metadata,
            SchemaVersion = schemaVersion,
            OperationId = entity.OperationId,
            RawPayloadHash = OptionalHash(entity.RawPayloadHash),
            ConfigurationPayloadHash = OptionalHash(entity.ConfigurationPayloadHash),
            ObservationPayloadHash = OptionalHash(entity.ObservationPayloadHash),
            CapabilityPayloadHash = OptionalHash(entity.CapabilityPayloadHash),
        };
    }

    private async Task<int> ResolveSchemaVersionAsync(
        SnapshotCaptureEntity entity,
        CancellationToken cancellationToken)
    {
        byte[]? payloadKey = entity.ConfigurationPayloadHash
            ?? entity.RawPayloadHash
            ?? entity.ObservationPayloadHash
            ?? entity.CapabilityPayloadHash;
        if (payloadKey is null)
        {
            return 0;
        }

        SnapshotPayloadEntity? payload = await _db.SnapshotPayloads
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PayloadHash == payloadKey, cancellationToken)
            .ConfigureAwait(false);
        return payload?.SchemaVersion ?? 0;
    }

    private static Hash256? OptionalHash(byte[]? bytes)
        => bytes is null ? null : Hash256.Create(bytes);

    private static string SerializeSectionResults(IReadOnlyList<CapturedSectionDescriptor> sections)
    {
        if (sections.Count == 0)
        {
            return "[]";
        }

        var payload = sections.Select(s => new
        {
            sectionId = s.SectionId,
            sectionVersion = s.SectionVersion,
            status = s.Status,
            ordered = s.Ordered,
        }).ToArray();
        return JsonSerializer.Serialize(payload);
    }

    internal static string EncodeCursor(DateTimeOffset completedAtUtc, Guid id)
    {
        Span<byte> buffer = stackalloc byte[24];
        BinaryPrimitives.WriteInt64BigEndian(buffer[..8], completedAtUtc.UtcTicks);
        id.TryWriteBytes(buffer[8..]);
        return ToBase64Url(buffer);
    }

    internal static (DateTimeOffset CompletedAtUtc, Guid Id) DecodeCursor(string cursor)
    {
        byte[] bytes;
        try
        {
            bytes = FromBase64Url(cursor);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid snapshot list cursor.", nameof(cursor), ex);
        }

        if (bytes.Length != 24)
        {
            throw new ArgumentException("Invalid snapshot list cursor length.", nameof(cursor));
        }

        long ticks = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(0, 8));
        Guid id = new(bytes.AsSpan(8, 16));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static string ToBase64Url(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}
