using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;

namespace Mfc.Domain.Canonicalization;

/// <summary>
/// SHA-256 hash contracts for sections, configuration, observations, and snapshots
/// (Vertical Slice §18, M1-21 AC#9–10, #12).
/// </summary>
public static class CanonicalHashContract
{
    public const string SectionPrefix = "mfc.section.v1";
    public const string ConfigurationPrefix = "mfc.configuration.v1";
    public const string ObservationsPrefix = "mfc.observations.v1";
    public const string SnapshotPrefix = "mfc.snapshot.v1";

    /// <summary>section_hash = SHA256("mfc.section.v1\\0" + section_id + "\\0" + canonical_section_bytes)</summary>
    public static Hash256 HashSection(string sectionId, ReadOnlySpan<byte> canonicalSectionBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, SectionPrefix);
        AppendNull(hasher);
        AppendUtf8(hasher, sectionId);
        AppendNull(hasher);
        hasher.AppendData(canonicalSectionBytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>Hashes a canonicalized section document.</summary>
    public static Hash256 HashSection(CanonicalSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return HashSection(section.SectionId, section.Utf8Bytes);
    }

    /// <summary>
    /// configuration_hash over ordered (section_id, section_hash) pairs.
    /// </summary>
    public static ConfigurationHash HashConfiguration(
        IEnumerable<(string SectionId, Hash256 SectionHash)> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        (string SectionId, Hash256 SectionHash)[] ordered = sections
            .OrderBy(static s => s.SectionId, StringComparer.Ordinal)
            .ToArray();

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, ConfigurationPrefix);
        AppendNull(hasher);
        foreach ((string sectionId, Hash256 sectionHash) in ordered)
        {
            AppendUtf8(hasher, sectionId);
            AppendNull(hasher);
            hasher.AppendData(sectionHash.Bytes);
        }

        return ConfigurationHash.FromDigest(Hash256.Create(hasher.GetHashAndReset()));
    }

    /// <summary>observation_hash over ordered observation section hashes.</summary>
    public static ObservationHash HashObservations(
        IEnumerable<(string SectionId, Hash256 SectionHash)> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        (string SectionId, Hash256 SectionHash)[] ordered = sections
            .OrderBy(static s => s.SectionId, StringComparer.Ordinal)
            .ToArray();

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, ObservationsPrefix);
        AppendNull(hasher);
        foreach ((string sectionId, Hash256 sectionHash) in ordered)
        {
            AppendUtf8(hasher, sectionId);
            AppendNull(hasher);
            hasher.AppendData(sectionHash.Bytes);
        }

        return ObservationHash.FromDigest(Hash256.Create(hasher.GetHashAndReset()));
    }

    /// <summary>
    /// snapshot_hash includes schema version plus configuration and observation hashes (M1-21 AC#10).
    /// </summary>
    public static SnapshotHash HashSnapshot(
        int schemaVersion,
        ConfigurationHash configurationHash,
        ObservationHash observationHash)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, SnapshotPrefix);
        AppendNull(hasher);
        AppendUtf8(hasher, schemaVersion.ToString(CultureInfo.InvariantCulture));
        AppendNull(hasher);
        hasher.AppendData(configurationHash.Value.Bytes);
        hasher.AppendData(observationHash.Value.Bytes);
        return SnapshotHash.FromDigest(Hash256.Create(hasher.GetHashAndReset()));
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendNull(IncrementalHash hasher)
        => hasher.AppendData([(byte)0]);
}
