using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using DomainDiffChange = Mfc.Domain.Diff.DiffChange;
using DomainDiffDomain = Mfc.Domain.Diff.DiffDomain;
using DomainMatchConfidence = Mfc.Domain.Diff.MatchConfidence;
using ProtoDiffChange = Mfc.Contracts.Mfc.V1.DiffChange;
using ProtoDiffDomain = Mfc.Contracts.Mfc.V1.DiffDomain;
using ProtoMatchConfidence = Mfc.Contracts.Mfc.V1.MatchConfidence;

namespace Mfc.Controller.Grpc;

/// <summary>Maps Application snapshot/diff views to <c>mfc.v1</c> protobuf messages.</summary>
public static class SnapshotProtoMapper
{
    public static SnapshotSummary ToProto(SnapshotView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        SnapshotSummary summary = new()
        {
            CaptureId = ProtoUuid.FromGuid(view.Id),
            DeviceId = ProtoUuid.FromGuid(view.DeviceId),
            Status = ToProto(view.Status),
            SchemaVersion = checked((uint)Math.Max(view.SchemaVersion, 0)),
        };
        if (TrySha256(view.ConfigurationHashHex, out Sha256? configuration))
        {
            summary.ConfigurationHash = configuration;
        }

        if (TrySha256(view.ObservationHashHex, out Sha256? observation))
        {
            summary.ObservationHash = observation;
        }

        if (TrySha256(view.CapabilityHashHex, out Sha256? capability))
        {
            summary.CapabilityHash = capability;
        }

        if (TrySha256(view.SnapshotHashHex, out Sha256? snapshot))
        {
            summary.SnapshotHash = snapshot;
        }

        if (view.CompletedAtUtc is DateTimeOffset completed)
        {
            summary.CompletedAt = Timestamp.FromDateTimeOffset(completed);
        }

        foreach (SnapshotSectionSummaryView section in view.Sections)
        {
            summary.Sections.Add(new SnapshotSectionSummary
            {
                SectionId = section.SectionId,
                Status = ToProtoSectionStatus(section.Status),
                Ordered = section.Ordered,
                ConfigurationRecordCount = checked((uint)Math.Max(section.ConfigurationRecordCount, 0)),
                ObservationRecordCount = checked((uint)Math.Max(section.ObservationRecordCount, 0)),
                CapabilityRecordCount = checked((uint)Math.Max(section.CapabilityRecordCount, 0)),
                CompatibilityRecordCount = checked((uint)Math.Max(section.CompatibilityRecordCount, 0)),
            });
        }

        return summary;
    }

    private static SnapshotSectionCaptureStatus ToProtoSectionStatus(short status) => status switch
    {
        1 => SnapshotSectionCaptureStatus.Ok,
        2 => SnapshotSectionCaptureStatus.Unsupported,
        3 => SnapshotSectionCaptureStatus.NotApplicable,
        4 => SnapshotSectionCaptureStatus.Failed,
        5 => SnapshotSectionCaptureStatus.PartialError,
        _ => SnapshotSectionCaptureStatus.Unspecified,
    };

    public static SnapshotSectionPage ToProto(SnapshotSectionPageView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        SnapshotSectionPage page = new()
        {
            CaptureId = ProtoUuid.FromGuid(view.CaptureId),
            SectionId = view.SectionId,
            Ordered = view.Ordered,
            NextPageToken = view.NextCursor ?? string.Empty,
        };
        page.Records.AddRange(view.Records.Select(ToProto));
        return page;
    }

    public static SnapshotRecord ToProto(SnapshotRecordView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        SnapshotRecord record = new()
        {
            StableKey = view.StableKey,
        };
        if (view.Ordinal is int ordinal && ordinal >= 0)
        {
            record.Ordinal = (uint)ordinal;
        }

        foreach ((string name, string value) in view.Configuration.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            record.Configuration.Add(new CanonicalField
            {
                Name = name,
                Value = new CanonicalValue { StringValue = value },
            });
        }

        foreach ((string name, string value) in view.Observations.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            record.Observations.Add(new CanonicalField
            {
                Name = name,
                Value = new CanonicalValue { StringValue = value },
            });
        }

        return record;
    }

    public static DiffEntry ToProto(SnapshotDiffEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        DiffEntry entry = new()
        {
            SectionId = view.SectionId,
            Domain = ParseDomain(view.Domain),
            Confidence = ParseConfidence(view.Confidence),
            RecordKey = view.RecordKey,
        };
        foreach (string change in view.Changes)
        {
            entry.Changes.Add(ParseChange(change));
        }

        if (view.BeforeOrdinal is int beforeOrdinal && beforeOrdinal >= 0)
        {
            entry.BeforeOrdinal = (uint)beforeOrdinal;
        }

        if (view.AfterOrdinal is int afterOrdinal && afterOrdinal >= 0)
        {
            entry.AfterOrdinal = (uint)afterOrdinal;
        }

        if (view.BeforeProps is not null)
        {
            entry.Before = PropsToRecord(view.RecordKey, view.BeforeOrdinal, view.BeforeProps, entry.Domain);
        }

        if (view.AfterProps is not null)
        {
            entry.After = PropsToRecord(view.RecordKey, view.AfterOrdinal, view.AfterProps, entry.Domain);
        }

        foreach (SnapshotDiffFieldChangeView field in view.FieldChanges)
        {
            FieldDiff diff = new() { FieldName = field.FieldName };
            if (field.Before is not null)
            {
                diff.Before = new CanonicalValue { StringValue = field.Before };
            }

            if (field.After is not null)
            {
                diff.After = new CanonicalValue { StringValue = field.After };
            }

            foreach (string added in field.AddedValues)
            {
                diff.AddedValues.Add(new CanonicalValue { StringValue = added });
            }

            foreach (string removed in field.RemovedValues)
            {
                diff.RemovedValues.Add(new CanonicalValue { StringValue = removed });
            }

            entry.FieldDiffs.Add(diff);
        }

        return entry;
    }

    public static DomainDiffDomain? ToDomain(ProtoDiffDomain domain)
        => domain switch
        {
            ProtoDiffDomain.Unspecified => null,
            ProtoDiffDomain.Configuration => DomainDiffDomain.Configuration,
            ProtoDiffDomain.Observation => DomainDiffDomain.Observation,
            ProtoDiffDomain.Capability => DomainDiffDomain.Capability,
            ProtoDiffDomain.Compatibility => DomainDiffDomain.Compatibility,
            _ => null,
        };

    /// <summary>Parses a 64-char hex digest into a 32-byte <see cref="Sha256"/> wire value.</summary>
    public static bool TrySha256(string? hex, out Sha256? sha)
    {
        sha = null;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            Hash256 digest = Hash256.ParseHex(hex);
            byte[] bytes = digest.Bytes.ToArray();
            if (bytes.Length != 32)
            {
                return false;
            }

            sha = new Sha256 { Value = ByteString.CopyFrom(bytes) };
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or DomainInvariantException)
        {
            return false;
        }
    }

    /// <summary>Hex helper exposed for unit tests and wire validation.</summary>
    public static byte[] HexToSha256Bytes(string hex)
    {
        Hash256 digest = Hash256.ParseHex(hex);
        byte[] bytes = digest.Bytes.ToArray();
        if (bytes.Length != 32)
        {
            throw new ArgumentException("SHA-256 digest must be exactly 32 bytes.");
        }

        return bytes;
    }

    private static SnapshotRecord PropsToRecord(
        string recordKey,
        int? ordinal,
        IReadOnlyDictionary<string, string> props,
        ProtoDiffDomain domain)
    {
        SnapshotRecordView view = new()
        {
            StableKey = recordKey,
            Ordinal = ordinal,
            Configuration = domain == ProtoDiffDomain.Configuration
                ? props
                : new Dictionary<string, string>(StringComparer.Ordinal),
            Observations = domain is ProtoDiffDomain.Observation or ProtoDiffDomain.Capability
                or ProtoDiffDomain.Compatibility
                ? props
                : new Dictionary<string, string>(StringComparer.Ordinal),
        };
        return ToProto(view);
    }

    private static SnapshotCaptureStatus ToProto(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Queued => SnapshotCaptureStatus.Queued,
        SnapshotStatus.Connecting => SnapshotCaptureStatus.Connecting,
        SnapshotStatus.Authenticating => SnapshotCaptureStatus.Authenticating,
        SnapshotStatus.ReadingPass1 => SnapshotCaptureStatus.ReadingPass1,
        SnapshotStatus.CanonicalizingPass1 => SnapshotCaptureStatus.CanonicalizingPass1,
        SnapshotStatus.ReadingPass2 => SnapshotCaptureStatus.ReadingPass2,
        SnapshotStatus.VerifyingStability => SnapshotCaptureStatus.VerifyingStability,
        SnapshotStatus.Persisting => SnapshotCaptureStatus.Persisting,
        SnapshotStatus.Completed => SnapshotCaptureStatus.Completed,
        SnapshotStatus.Failed => SnapshotCaptureStatus.Failed,
        SnapshotStatus.Canceled => SnapshotCaptureStatus.Canceled,
        _ => SnapshotCaptureStatus.Unspecified,
    };

    private static ProtoDiffDomain ParseDomain(string domain)
    {
        if (System.Enum.TryParse(domain, ignoreCase: true, out DomainDiffDomain parsed))
        {
            return parsed switch
            {
                DomainDiffDomain.Configuration => ProtoDiffDomain.Configuration,
                DomainDiffDomain.Observation => ProtoDiffDomain.Observation,
                DomainDiffDomain.Capability => ProtoDiffDomain.Capability,
                DomainDiffDomain.Compatibility => ProtoDiffDomain.Compatibility,
                _ => ProtoDiffDomain.Unspecified,
            };
        }

        return ProtoDiffDomain.Unspecified;
    }

    private static ProtoDiffChange ParseChange(string change)
    {
        if (System.Enum.TryParse(change, ignoreCase: true, out DomainDiffChange parsed))
        {
            return parsed switch
            {
                DomainDiffChange.Added => ProtoDiffChange.Added,
                DomainDiffChange.Removed => ProtoDiffChange.Removed,
                DomainDiffChange.Modified => ProtoDiffChange.Modified,
                DomainDiffChange.Moved => ProtoDiffChange.Moved,
                DomainDiffChange.StateChanged => ProtoDiffChange.StateChanged,
                DomainDiffChange.SectionStatusChanged => ProtoDiffChange.SectionStatusChanged,
                _ => ProtoDiffChange.Unspecified,
            };
        }

        return ProtoDiffChange.Unspecified;
    }

    private static ProtoMatchConfidence ParseConfidence(string confidence)
    {
        if (System.Enum.TryParse(confidence, ignoreCase: true, out DomainMatchConfidence parsed))
        {
            return parsed switch
            {
                DomainMatchConfidence.ControllerId => ProtoMatchConfidence.ControllerId,
                DomainMatchConfidence.NaturalKey => ProtoMatchConfidence.NaturalKey,
                DomainMatchConfidence.ExactFingerprint => ProtoMatchConfidence.ExactFingerprint,
                DomainMatchConfidence.ExactSequence => ProtoMatchConfidence.ExactSequence,
                DomainMatchConfidence.Conservative => ProtoMatchConfidence.Conservative,
                _ => ProtoMatchConfidence.Unspecified,
            };
        }

        return ProtoMatchConfidence.Unspecified;
    }
}
