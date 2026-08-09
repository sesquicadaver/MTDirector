using Mfc.Domain.Canonicalization;
using Mfc.Domain.Snapshots;

namespace Mfc.Application.Snapshots;

/// <summary>
/// Application-facing facade over domain canonicalization primitives (M1-21).
/// Menu-specific discovery→canonical projection lives in <c>Mfc.RouterOs.Snapshot.DiscoveryCanonicalProjector</c> (M1-22).
/// </summary>
public static class CanonicalizationService
{
    /// <summary>Canonicalizes a section and returns deterministic UTF-8 bytes.</summary>
    public static CanonicalSection CanonicalizeSection(CanonicalSectionInput input)
        => Canonicalizer.Canonicalize(input);

    /// <summary>Builds separate configuration and observation hashes, then the snapshot hash.</summary>
    public static SnapshotHashBundle HashSnapshotBundle(
        int schemaVersion,
        IEnumerable<CanonicalSection> configurationSections,
        IEnumerable<CanonicalSection> observationSections)
    {
        ArgumentNullException.ThrowIfNull(configurationSections);
        ArgumentNullException.ThrowIfNull(observationSections);

        ConfigurationHash configurationHash = CanonicalHashContract.HashConfiguration(
            configurationSections.Select(static s => (s.SectionId, CanonicalHashContract.HashSection(s))));

        ObservationHash observationHash = CanonicalHashContract.HashObservations(
            observationSections.Select(static s => (s.SectionId, CanonicalHashContract.HashSection(s))));

        SnapshotHash snapshotHash = CanonicalHashContract.HashSnapshot(
            schemaVersion,
            configurationHash,
            observationHash);

        return new SnapshotHashBundle
        {
            SchemaVersion = schemaVersion,
            ConfigurationHash = configurationHash,
            ObservationHash = observationHash,
            SnapshotHash = snapshotHash,
        };
    }
}

/// <summary>Paired configuration / observation / snapshot hashes for one capture.</summary>
public sealed class SnapshotHashBundle
{
    public required int SchemaVersion { get; init; }

    public required ConfigurationHash ConfigurationHash { get; init; }

    public required ObservationHash ObservationHash { get; init; }

    public required SnapshotHash SnapshotHash { get; init; }
}
