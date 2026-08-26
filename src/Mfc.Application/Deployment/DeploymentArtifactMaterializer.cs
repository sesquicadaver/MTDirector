using Mfc.Domain.Deployment;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Staging drafts loaded from a sealed deployment artifact hash (P2-08).</summary>
public sealed class DeploymentStagingArtifacts
{
    public required IReadOnlyList<AddressListArtifactDraft> AddressLists { get; init; }

    public required IReadOnlyList<ChainArtifactDraft> Chains { get; init; }
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
        });
    }
}
