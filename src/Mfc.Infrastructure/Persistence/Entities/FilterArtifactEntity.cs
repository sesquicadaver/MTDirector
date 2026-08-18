namespace Mfc.Infrastructure.Persistence.Entities;

/// <summary>
/// Content-addressed RouterOS filter artifact body keyed by resource_hash (M3-07).
/// </summary>
public sealed class FilterArtifactEntity
{
    public required byte[] ResourceHash { get; set; }

    public required string ArtifactId { get; set; }

    public required Guid DeviceId { get; set; }

    public required byte[] PhysicalSemanticsHash { get; set; }

    public required byte[] CompilerProfileHash { get; set; }

    public required byte[] LogicalEffectivePolicyHash { get; set; }

    public required byte[] DeviceResolvedPolicyHash { get; set; }

    public required byte[] AnalysisBundleHash { get; set; }

    public required byte[] CapabilityHash { get; set; }

    public required string CompilerVersion { get; set; }

    public DateTimeOffset CompiledAtUtc { get; set; }

    public short Compression { get; set; }

    public long UncompressedSize { get; set; }

    public required byte[] CompressedPayload { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
