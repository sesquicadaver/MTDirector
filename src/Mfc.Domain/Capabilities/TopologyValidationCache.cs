namespace Mfc.Domain.Capabilities;

/// <summary>
/// Tracks whether cached topology validation remains valid for a device capability (M1-17 AC#9).
/// Topology validation itself is M1-18; this only encodes the invalidation rule.
/// </summary>
public sealed class TopologyValidationCache
{
    private CapabilityHash? _validatedAgainst;

    public CapabilityHash? ValidatedAgainst => _validatedAgainst;

    public bool IsValidFor(CapabilityHash current)
        => _validatedAgainst is { } previous && previous.Equals(current);

    /// <summary>Marks topology validation as current for <paramref name="capabilityHash"/>.</summary>
    public void RememberValidated(CapabilityHash capabilityHash)
        => _validatedAgainst = capabilityHash;

    /// <summary>
    /// Invalidates the cache when capability changes. Returns true when a previous validation was cleared.
    /// </summary>
    public bool InvalidateIfCapabilityChanged(CapabilityHash current)
    {
        if (_validatedAgainst is not { } previous)
        {
            return false;
        }

        if (previous.Equals(current))
        {
            return false;
        }

        _validatedAgainst = null;
        return true;
    }

    public void Clear() => _validatedAgainst = null;
}
