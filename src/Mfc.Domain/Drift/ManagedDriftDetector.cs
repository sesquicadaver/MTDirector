using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Drift;

/// <summary>
/// Pure managed drift detector: baseline is last committed artifact only (E2E §32–§33 / M6-02).
/// Desired policy is never used as the actual baseline.
/// </summary>
public static class ManagedDriftDetector
{
    /// <summary>
    /// Evaluates managed configuration drift from committed/actual hashes and typed findings.
    /// <paramref name="desiredArtifactHash"/> may differ for pending deploy and must not become the baseline.
    /// </summary>
    public static DriftEvaluation Evaluate(
        Hash256? lastCommittedArtifactHash,
        Hash256? actualManagedResourceHash,
        Hash256? desiredArtifactHash,
        IReadOnlyList<DriftFinding> findings,
        string? semanticDiffCanonical = null)
    {
        ArgumentNullException.ThrowIfNull(findings);

        DriftFinding[] normalized = findings
            .OrderBy(static f => (byte)f.Kind)
            .ThenBy(static f => f.Detail, StringComparer.Ordinal)
            .ToArray();

        Hash256? semanticHash = HashSemanticDiff(semanticDiffCanonical);

        bool hashesComparable = lastCommittedArtifactHash is not null && actualManagedResourceHash is not null;
        bool hashDiverged = hashesComparable
                            && !lastCommittedArtifactHash!.Equals(actualManagedResourceHash!);
        bool hashesMatch = hashesComparable
                           && lastCommittedArtifactHash!.Equals(actualManagedResourceHash!);

        // Desired may differ while actual still equals committed → pending deploy, never baseline.
        bool pendingDeploy = hashesMatch
                             && desiredArtifactHash is not null
                             && lastCommittedArtifactHash is not null
                             && !desiredArtifactHash.Equals(lastCommittedArtifactHash);

        bool anyCritical = normalized.Any(static f => f.Severity == DriftSeverity.Critical);
        bool anyWarning = normalized.Any(static f => f.Severity == DriftSeverity.Warning);
        bool onlyObservationOrIgnored = normalized.Length > 0
                                        && normalized.All(static f =>
                                            f.Severity is DriftSeverity.Observation or DriftSeverity.Ignored);

        // Configuration drift = actual diverges from last committed (desired is never the baseline).
        bool configurationDrift = hashDiverged;

        DriftOutcome outcome;
        if (configurationDrift || anyCritical)
        {
            outcome = DriftOutcome.CriticalDrift;
        }
        else if (pendingDeploy)
        {
            // AC2: desired≠committed with actual==committed is NOT configuration drift.
            outcome = DriftOutcome.PendingDeploymentNotDrift;
        }
        else if (anyWarning)
        {
            outcome = DriftOutcome.WarningDrift;
        }
        else if (onlyObservationOrIgnored)
        {
            outcome = DriftOutcome.ObservationOnly;
        }
        else
        {
            outcome = DriftOutcome.NoDrift;
        }

        bool blocks = outcome == DriftOutcome.CriticalDrift;

        return new DriftEvaluation(
            outcome,
            configurationDriftPresent: configurationDrift,
            blocksDeployment: blocks,
            baselineCommittedHash: lastCommittedArtifactHash,
            actualManagedResourceHash: actualManagedResourceHash,
            desiredArtifactHashIgnoredForBaseline: desiredArtifactHash,
            findings: normalized,
            semanticDiffCanonical: semanticDiffCanonical,
            semanticDiffHash: semanticHash);
    }

    private static Hash256? HashSemanticDiff(string? canonical)
    {
        if (string.IsNullOrEmpty(canonical))
        {
            return null;
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Hash256.Create(digest);
    }
}
