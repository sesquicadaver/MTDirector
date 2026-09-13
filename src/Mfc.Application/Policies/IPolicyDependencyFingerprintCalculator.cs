using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>Inputs for Controller-authoritative dependency fingerprint computation (AUDIT-AN-02).</summary>
public sealed class DependencyFingerprintRequest
{
    public required string AnalyzerVersion { get; init; }

    public required string PolicySchemaVersion { get; init; }

    public required string PipelineVersion { get; init; }

    public NodeId? NodeId { get; init; }

    /// <summary>
    /// Frozen fingerprint from an existing analysis run. Passthrough calculators echo this;
    /// live calculators ignore it and recompute from stores.
    /// </summary>
    public Hash256? FrozenRunFingerprint { get; init; }
}

/// <summary>Computes the Controller-authoritative current approval dependency fingerprint (AUDIT-AN-02).</summary>
public interface IPolicyDependencyFingerprintCalculator
{
    Task<Hash256> ComputeCurrentAsync(
        DependencyFingerprintRequest request,
        CancellationToken cancellationToken = default);
}
