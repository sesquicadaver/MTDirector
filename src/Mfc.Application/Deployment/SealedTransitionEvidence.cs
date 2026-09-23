using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>
/// Proves intermediate old/new transition states from sealed filter artifact bodies (AUDIT-EVID-01).
/// Does not invent <see cref="TransitionStateValidator.AllSafeEvidence"/>.
/// </summary>
public static class SealedTransitionEvidence
{
    /// <summary>
    /// For each state 0..N, marks safe only when every jump target resolves to a known chain
    /// in the corresponding sealed body (or a bootstrap root when old is bootstrap).
    /// Unknown/missing targets yield unsafe evidence that blocks plan sealing.
    /// </summary>
    public static IReadOnlyList<TransitionStateEvidence> Prove(
        IReadOnlyList<AnchorKey> activationOrder,
        IReadOnlyList<AnchorTarget> oldTargets,
        IReadOnlyList<AnchorTarget> newTargets,
        RouterOsFilterArtifactReader.ParsedBody? oldBody,
        RouterOsFilterArtifactReader.ParsedBody newBody,
        bool oldIsBootstrap)
    {
        ArgumentNullException.ThrowIfNull(activationOrder);
        ArgumentNullException.ThrowIfNull(oldTargets);
        ArgumentNullException.ThrowIfNull(newTargets);
        ArgumentNullException.ThrowIfNull(newBody);
        if (activationOrder.Count < 1)
        {
            throw new DomainInvariantException("activation count must be >= 1.");
        }

        if (!oldIsBootstrap && oldBody is null)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.SealedEvidenceMissing}: committed old artifact body is required.");
        }

        HashSet<string> newChains = ChainNames(newBody);
        HashSet<string> oldChains = oldIsBootstrap
            ? BootstrapRoots(activationOrder)
            : ChainNames(oldBody!);

        Dictionary<string, string> oldBy = oldTargets.ToDictionary(
            static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal);
        Dictionary<string, string> newBy = newTargets.ToDictionary(
            static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal);

        TransitionStateEvidence[] items = new TransitionStateEvidence[activationOrder.Count + 1];
        for (int state = 0; state <= activationOrder.Count; state++)
        {
            bool safe = true;
            string? detail = null;
            for (int a = 0; a < activationOrder.Count; a++)
            {
                AnchorKey key = activationOrder[a];
                bool useNew = a < state;
                if (!oldBy.TryGetValue(key.Marker, out string? oldJump)
                    || !newBy.TryGetValue(key.Marker, out string? newJump))
                {
                    safe = false;
                    detail = DeploymentCodes.AnchorInvalid;
                    break;
                }

                string jump = useNew ? newJump : oldJump;
                HashSet<string> allowed = useNew ? newChains : oldChains;
                if (!allowed.Contains(jump))
                {
                    safe = false;
                    detail = DeploymentCodes.TransitionStateUnsafe;
                    break;
                }
            }

            items[state] = new TransitionStateEvidence(state, safe, detail);
        }

        return items;
    }

    private static HashSet<string> ChainNames(RouterOsFilterArtifactReader.ParsedBody body)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (ChainArtifactDraft chain in body.Chains)
        {
            if (!string.IsNullOrWhiteSpace(chain.Name))
            {
                names.Add(chain.Name.Trim());
            }
        }

        return names;
    }

    private static HashSet<string> BootstrapRoots(IReadOnlyList<AnchorKey> activationOrder)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (AnchorKey key in activationOrder)
        {
            names.Add(BootstrapArtifact.RootChainName(key.Family, key.Chain));
        }

        return names;
    }
}
