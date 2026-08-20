namespace Mfc.Domain.Drift;

/// <summary>
/// Fail-closed allowlist for temporary disabled watchdog residue cleanup (E2E §49 / M6-03).
/// Cleanup may remove only temporary watchdog resources — never firewall artifacts, snapshots, or audit.
/// </summary>
public static class WatchdogResidueCleanupPolicy
{
    /// <summary>Whether <paramref name="resourceName"/> is a temporary watchdog / capability proof name.</summary>
    public static bool IsAllowedTemporaryWatchdogResource(string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return false;
        }

        string name = resourceName.Trim();
        return Deployment.DeploymentWatchdogNames.IsDeploymentWatchdogName(name)
               || Onboarding.OnboardingWatchdogNames.IsOnboardingWatchdogName(name)
               || Onboarding.OnboardingWatchdogNames.IsCapabilityProofName(name);
    }

    /// <summary>
    /// Names that must never be deleted by the residue cleanup job
    /// (firewall managed chains, address lists, snapshots, audit, approved revisions).
    /// </summary>
    public static bool IsForbiddenCleanupTarget(string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return true;
        }

        string name = resourceName.Trim();
        if (IsAllowedTemporaryWatchdogResource(name))
        {
            return false;
        }

        // Managed filter namespaces / roots (Compiler Spec §8).
        if (name.StartsWith("mfc4.", StringComparison.Ordinal)
            || name.StartsWith("mfc6.", StringComparison.Ordinal)
            || name.StartsWith("mfc.", StringComparison.Ordinal)
            || name.StartsWith("fwc.", StringComparison.Ordinal))
        {
            return true;
        }

        // Explicit forbidden categories from E2E §49.
        if (name.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("audit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("approved", StringComparison.OrdinalIgnoreCase)
            || name.Contains("filter", StringComparison.OrdinalIgnoreCase)
            || name.Contains("address-list", StringComparison.OrdinalIgnoreCase)
            || name.Contains("address_list", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Anything not on the temporary-watchdog allowlist is forbidden.
        return true;
    }

    /// <summary>Throws when a cleanup candidate is outside the temporary disabled watchdog allowlist.</summary>
    public static void EnsureAllowed(string resourceName)
    {
        if (IsForbiddenCleanupTarget(resourceName) || !IsAllowedTemporaryWatchdogResource(resourceName))
        {
            throw new DomainInvariantException(
                $"Watchdog residue cleanup refused forbidden target '{resourceName}'.");
        }
    }
}
