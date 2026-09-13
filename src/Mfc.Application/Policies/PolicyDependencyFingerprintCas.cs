using Mfc.Application.Common;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>
/// AUDIT-AN-02: server computes current dependency fingerprint; client bytes are CAS expectation only.
/// </summary>
public static class PolicyDependencyFingerprintCas
{
    /// <summary>
    /// Validates client CAS against <paramref name="serverCurrent"/> and whether the analysis run is fresh.
    /// </summary>
    public static ApplicationError? Evaluate(
        Hash256 runFingerprint,
        Hash256 clientExpectedCurrent,
        Hash256 serverCurrent,
        string staleCode,
        string casMismatchMessage)
    {
        ArgumentNullException.ThrowIfNull(runFingerprint);
        ArgumentNullException.ThrowIfNull(clientExpectedCurrent);
        ArgumentNullException.ThrowIfNull(serverCurrent);
        ArgumentException.ThrowIfNullOrWhiteSpace(staleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(casMismatchMessage);

        if (!clientExpectedCurrent.Equals(serverCurrent))
        {
            return new ApplicationError(staleCode, casMismatchMessage);
        }

        if (!runFingerprint.Equals(serverCurrent))
        {
            return new ApplicationError(
                staleCode,
                "Analysis dependency fingerprint is stale relative to server-computed current dependencies.");
        }

        return null;
    }

    /// <summary>True when the frozen run fingerprint still matches server-computed current.</summary>
    public static bool IsAnalysisCurrent(Hash256 runFingerprint, Hash256 serverCurrent)
    {
        ArgumentNullException.ThrowIfNull(runFingerprint);
        ArgumentNullException.ThrowIfNull(serverCurrent);
        return runFingerprint.Equals(serverCurrent);
    }
}
