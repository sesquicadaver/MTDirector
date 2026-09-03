using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Staging drafts loaded from a sealed deployment artifact hash (P2-08 / SEC-02).</summary>
public sealed class DeploymentStagingArtifacts
{
    public required IReadOnlyList<AddressListArtifactDraft> AddressLists { get; init; }

    public required IReadOnlyList<ChainArtifactDraft> Chains { get; init; }

    /// <summary>
    /// Sealed expected artifact when loaded from <see cref="IFilterArtifactStore"/>; null for AnchorOnly harnesses.
    /// </summary>
    public RouterOsFilterArtifact? SealedArtifact { get; init; }
}

/// <summary>Resolves device-plan artifact hashes into staging drafts for deployment writers.</summary>
public interface IDeploymentArtifactMaterializer
{
    Task<DeploymentStagingArtifacts> LoadAsync(
        DeviceDeploymentPlan devicePlan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Anchor-only fallback when artifact bodies are not required (no detached staging deltas).
/// Uses empty address-list/chain drafts; hash verification relies on the plan's sealed hashes.
/// </summary>
public sealed class AnchorOnlyDeploymentArtifactMaterializer : IDeploymentArtifactMaterializer
{
    public Task<DeploymentStagingArtifacts> LoadAsync(
        DeviceDeploymentPlan devicePlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DeploymentStagingArtifacts
        {
            AddressLists = [],
            Chains = [],
            SealedArtifact = null,
        });
    }
}

/// <summary>
/// Production materializer: loads AddressLists/Chains from content-addressed filter artifact store (SEC-02).
/// Fail-closed when the sealed body is missing or does not match <see cref="DeviceDeploymentPlan.NewArtifactHash"/>.
/// </summary>
public sealed class FilterArtifactStoreDeploymentArtifactMaterializer : IDeploymentArtifactMaterializer
{
    private readonly IFilterArtifactStore _artifacts;

    public FilterArtifactStoreDeploymentArtifactMaterializer(IFilterArtifactStore artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        _artifacts = artifacts;
    }

    public async Task<DeploymentStagingArtifacts> LoadAsync(
        DeviceDeploymentPlan devicePlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? canonical = await _artifacts
            .GetCanonicalBytesByResourceHashAsync(devicePlan.NewArtifactHash, cancellationToken)
            .ConfigureAwait(false);
        if (canonical is null || canonical.Length == 0)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ActiveArtifactHashMismatch}: sealed filter artifact body is missing for new hash.");
        }

        StoredFilterArtifact? meta = await _artifacts
            .GetByResourceHashAsync(devicePlan.NewArtifactHash, cancellationToken)
            .ConfigureAwait(false);
        if (meta is null)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ActiveArtifactHashMismatch}: sealed filter artifact metadata is missing for new hash.");
        }

        RouterOsFilterArtifactReader.ParsedBody body = RouterOsFilterArtifactReader.Read(canonical);
        RouterOsFilterArtifact sealedArtifact = RouterOsFilterArtifact.Create(
            meta.CompilerProfileHash,
            meta.PhysicalSemanticsHash,
            meta.DeviceId,
            body.AddressLists,
            body.Chains,
            body.Anchors,
            body.LayoutVersion);
        if (!sealedArtifact.ResourceHash.Equals(devicePlan.NewArtifactHash))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ActiveArtifactHashMismatch}: resealed filter artifact hash diverges from plan.");
        }

        return new DeploymentStagingArtifacts
        {
            AddressLists = body.AddressLists,
            Chains = body.Chains,
            SealedArtifact = sealedArtifact,
        };
    }
}
