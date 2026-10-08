using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.Application.Deployment;

/// <summary>
/// Live RouterOS recheck before standalone staging (EVID-LIVE-01 / F02 residual).
/// Mirrors VRRP <c>PrecheckAsync</c> old-anchor read and additionally requires the observed
/// managed resource hash to match the sealed <see cref="DeviceDeploymentPlan.OldArtifactHash"/>.
/// </summary>
public static class StandaloneLiveRecheck
{
    /// <summary>
    /// Reads live managed state; fails closed when old anchors or old artifact hash diverge from the sealed plan.
    /// </summary>
    public static async Task ExecuteAsync(
        DeviceDeploymentPlan devicePlan,
        IStandaloneDeploymentDeviceRuntime runtime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(runtime);

        ActualManagedState state = await runtime.Session
            .ReadManagedStateAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, string> jumps = ManagedResourceHashObservation.ExtractAnchorJumps(state);
        foreach (AnchorTarget expected in devicePlan.OldAnchorTargets)
        {
            if (!jumps.TryGetValue(expected.Key.Marker, out string? actual)
                || !string.Equals(actual, expected.JumpTarget, StringComparison.Ordinal))
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.AnchorPreconditionFailed}: live old anchor '{expected.Key.Marker}' does not match the sealed plan.");
            }
        }

        // When old==new (NO_CHANGES), ClassifyAnchors cannot return AllOld; anchors already matched sealed old.
        if (devicePlan.OldArtifactHash.Equals(devicePlan.NewArtifactHash))
        {
            return;
        }

        // Distinct old/new: observe managed hash via runtime (production loads sealed old body).
        Hash256 observed = await runtime.ReadManagedResourceHashAsync(cancellationToken).ConfigureAwait(false);
        if (!observed.Equals(devicePlan.OldArtifactHash))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ActiveArtifactHashMismatch}: live managed resource hash does not match sealed old artifact.");
        }
    }
}
