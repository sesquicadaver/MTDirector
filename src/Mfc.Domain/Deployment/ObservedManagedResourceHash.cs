using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Measures managed <c>resource_hash</c> from live RouterOS rows against a sealed expected artifact (SEC-02).
/// Fail-closed: every expected list/chain must match live content; anchors use observed jump-targets.
/// </summary>
public static class ObservedManagedResourceHash
{
    /// <summary>
    /// Reseals the expected lists/chains with <paramref name="observedJumpByMarker"/> anchors and returns
    /// the resulting <see cref="RouterOsFilterArtifact.ResourceHash"/>.
    /// </summary>
    public static bool TryCompute(
        RouterOsFilterArtifact expected,
        IReadOnlyList<ActualAddressListEntry> addressListRows,
        IReadOnlyList<ActualFilterChainRule> filterRules,
        IReadOnlyDictionary<string, string> observedJumpByMarker,
        out Hash256 observedHash,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(addressListRows);
        ArgumentNullException.ThrowIfNull(filterRules);
        ArgumentNullException.ThrowIfNull(observedJumpByMarker);

        List<AddressListArtifactDraft> listDrafts = [];
        foreach (AddressListArtifact list in expected.AddressLists)
        {
            AddressListArtifactDraft draft = RouterOsFilterArtifactReader.ToDraft(list);
            if (!AddressListCreateOrVerify.TryVerifyContentHash(draft, addressListRows, out _, out string? listError))
            {
                observedHash = Hash256.Create(new byte[Hash256.Size]);
                error = listError ?? DeploymentCodes.ActiveArtifactHashMismatch;
                return false;
            }

            listDrafts.Add(draft);
        }

        List<ChainArtifactDraft> chainDrafts = [];
        foreach (ChainArtifact chain in expected.Chains)
        {
            ChainArtifactDraft draft = RouterOsFilterArtifactReader.ToDraft(chain);
            if (!FilterChainCreateOrVerify.TryVerifyChainHash(draft, filterRules, out _, out string? chainError))
            {
                observedHash = Hash256.Create(new byte[Hash256.Size]);
                error = chainError ?? DeploymentCodes.ActiveArtifactHashMismatch;
                return false;
            }

            chainDrafts.Add(draft);
        }

        List<AnchorTargetArtifact> observedAnchors = [];
        foreach (AnchorTargetArtifact expectedAnchor in expected.AnchorTargets)
        {
            if (!observedJumpByMarker.TryGetValue(expectedAnchor.ExpectedAnchorComment, out string? jump)
                || string.IsNullOrWhiteSpace(jump))
            {
                observedHash = Hash256.Create(new byte[Hash256.Size]);
                error = DeploymentCodes.ActiveArtifactHashMismatch;
                return false;
            }

            observedAnchors.Add(AnchorTargetArtifact.Create(
                expectedAnchor.Family,
                expectedAnchor.BuiltInChain,
                expectedAnchor.ExpectedAnchorComment,
                jump.Trim()));
        }

        try
        {
            RouterOsFilterArtifact sealedObserved = RouterOsFilterArtifact.Create(
                expected.CompilerProfileHash,
                expected.PhysicalSemanticsHash,
                expected.DeviceId,
                listDrafts,
                chainDrafts,
                observedAnchors,
                expected.LayoutVersion);
            observedHash = sealedObserved.ResourceHash;
            error = null;
            return true;
        }
        catch (DomainInvariantException ex)
        {
            observedHash = Hash256.Create(new byte[Hash256.Size]);
            error = ex.Message;
            return false;
        }
    }
}
