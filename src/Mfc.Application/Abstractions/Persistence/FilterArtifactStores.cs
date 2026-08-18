using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Content-addressed RouterOS filter artifact store (Compiler Spec §6 / M3-07).</summary>
public interface IFilterArtifactStore
{
    /// <summary>Returns the stored body when <paramref name="resourceHash"/> already exists.</summary>
    Task<StoredFilterArtifact?> GetByResourceHashAsync(
        Hash256 resourceHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts when absent. When the hash already exists, returns the existing row without duplicating bytes.
    /// </summary>
    Task<StoredFilterArtifact> PutIfAbsentAsync(
        RouterOsFilterArtifact artifact,
        CompilationProvenance provenance,
        CancellationToken cancellationToken = default);
}

/// <summary>Persisted filter artifact projection (semantic fields only for Application consumers).</summary>
public sealed class StoredFilterArtifact
{
    public required Hash256 ResourceHash { get; init; }

    public required string ArtifactId { get; init; }

    public required DeviceId DeviceId { get; init; }

    public required Hash256 PhysicalSemanticsHash { get; init; }

    public required Hash256 CompilerProfileHash { get; init; }

    public required Hash256 LogicalEffectivePolicyHash { get; init; }

    public required Hash256 DeviceResolvedPolicyHash { get; init; }

    public required Hash256 AnalysisBundleHash { get; init; }

    public required Hash256 CapabilityHash { get; init; }

    public required string CompilerVersion { get; init; }

    public required DateTimeOffset CompiledAtUtc { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required long UncompressedSize { get; init; }

    /// <summary>True when this call inserted a new row; false when an identical hash already existed.</summary>
    public required bool Inserted { get; init; }
}
