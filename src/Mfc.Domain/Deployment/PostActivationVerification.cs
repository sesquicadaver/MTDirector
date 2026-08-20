using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>One post-activation verification finding (Safe Deployment Spec §32–§34).</summary>
public sealed class DeploymentVerificationFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Target { get; init; }

    public bool RequiresRollback { get; init; }
}

/// <summary>Outcome of pure post-activation checks before I/O probes complete.</summary>
public sealed class ManagedIntegrityResult
{
    public required IReadOnlyList<DeploymentVerificationFinding> Findings { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != DeploymentCodes.SeverityBlocker);

    public bool RequiresRollback => Findings.Any(static f => f.RequiresRollback);
}

/// <summary>
/// Pure managed-resource integrity and probe-profile gates (Safe Deployment Spec §32.1 / §33–§34 / M4-07).
/// </summary>
public static class PostActivationVerification
{
    public const string AnalyzerVersion = "mfc.deployment.verify.v1";

    /// <summary>Verify active permanent-anchor jump-targets equal the plan's desired new set (AC#2).</summary>
    public static ManagedIntegrityResult VerifyActiveAnchors(
        IReadOnlyList<AnchorTarget> expectedNewTargets,
        IReadOnlyDictionary<string, string> observedJumpByMarker)
    {
        ArgumentNullException.ThrowIfNull(expectedNewTargets);
        ArgumentNullException.ThrowIfNull(observedJumpByMarker);
        List<DeploymentVerificationFinding> findings = [];
        foreach (AnchorTarget expected in expectedNewTargets.OrderBy(static t => t.Key.Marker, StringComparer.Ordinal))
        {
            if (!observedJumpByMarker.TryGetValue(expected.Key.Marker, out string? observed)
                || string.IsNullOrWhiteSpace(observed))
            {
                findings.Add(Blocker(
                    DeploymentCodes.AnchorInvalid,
                    "Active anchor is missing after activation.",
                    expected.Key.Marker,
                    requiresRollback: true));
                continue;
            }

            if (!string.Equals(observed.Trim(), expected.JumpTarget, StringComparison.Ordinal))
            {
                findings.Add(Blocker(
                    DeploymentCodes.ActiveArtifactHashMismatch,
                    "Active anchor jump-target does not match the desired new target.",
                    expected.Key.Marker,
                    requiresRollback: true));
            }
        }

        return new ManagedIntegrityResult { Findings = findings };
    }

    /// <summary>Compare observed managed resource hash to the sealed plan new artifact hash (AC#1).</summary>
    public static ManagedIntegrityResult VerifyManagedResourceHash(Hash256 expectedNewArtifactHash, Hash256 observedResourceHash)
    {
        ArgumentNullException.ThrowIfNull(expectedNewArtifactHash);
        ArgumentNullException.ThrowIfNull(observedResourceHash);
        if (expectedNewArtifactHash.Equals(observedResourceHash))
        {
            return new ManagedIntegrityResult { Findings = [] };
        }

        return new ManagedIntegrityResult
        {
            Findings =
            [
                Blocker(
                    DeploymentCodes.ActiveArtifactHashMismatch,
                    "Observed managed resource hash does not match the plan new artifact hash.",
                    "resource_hash",
                    requiresRollback: true),
            ],
        };
    }

    /// <summary>Probe profile may only contain API_SSL and ROUTER_PING; destinations are literal IPs (AC#5–#8).</summary>
    public static ManagedIntegrityResult ValidateProbeProfile(IReadOnlyList<DeploymentProbe> probes)
    {
        ArgumentNullException.ThrowIfNull(probes);
        List<DeploymentVerificationFinding> findings = [];
        foreach (DeploymentProbe probe in probes)
        {
            ArgumentNullException.ThrowIfNull(probe);
            if (probe.Kind is not (DeploymentProbeKind.ApiSsl or DeploymentProbeKind.RouterPing))
            {
                findings.Add(Blocker(
                    DeploymentCodes.ProbeKindUnsupported,
                    "Unsupported probe kind.",
                    probe.Kind.ToString(),
                    requiresRollback: false));
            }

            if (probe.TimeoutMilliseconds is < DeploymentProbe.MinTimeoutMs or > DeploymentProbe.MaxTimeoutMs)
            {
                findings.Add(Blocker(
                    DeploymentCodes.DeploymentProbeFailed,
                    "Probe timeout is out of bounds.",
                    probe.Destination,
                    requiresRollback: false));
            }
        }

        return new ManagedIntegrityResult { Findings = findings };
    }

    /// <summary>Map critical probe outcomes to rollback (AC#9 / Spec §34).</summary>
    public static DeploymentVerificationFinding? ClassifyCriticalProbeOutcome(
        DeploymentProbeKind kind,
        string destination,
        string outcome)
    {
        string normalized = outcome.Trim().ToUpperInvariant();
        return normalized switch
        {
            "PASS" or "NOT_APPLICABLE" => null,
            "FAIL" => Blocker(
                DeploymentCodes.DeploymentProbeFailed,
                $"Critical {kind} probe failed.",
                destination,
                requiresRollback: true),
            "INCONCLUSIVE" => Blocker(
                DeploymentCodes.DeploymentProbeInconclusive,
                $"Critical {kind} probe was inconclusive.",
                destination,
                requiresRollback: true),
            _ => Blocker(
                DeploymentCodes.DeploymentProbeInconclusive,
                $"Critical {kind} probe returned an unknown outcome.",
                destination,
                requiresRollback: true),
        };
    }

    /// <summary>Watchdog must still be armed with commit margin before commit (AC#11 / Spec §32.4).</summary>
    public static ManagedIntegrityResult VerifyWatchdogReadiness(
        TimeSpan remainingTtl,
        bool deadlineSchedulerPresent,
        bool deadlineSchedulerEnabled,
        bool startupSchedulerPresent)
    {
        List<DeploymentVerificationFinding> findings = [];
        if (remainingTtl < DeploymentCodes.MinCommitMargin)
        {
            findings.Add(Blocker(
                DeploymentCodes.WatchdogDeadlineTooClose,
                "Watchdog remaining TTL is below the 30s commit margin.",
                "ttl",
                requiresRollback: true));
        }

        if (!deadlineSchedulerPresent || !startupSchedulerPresent)
        {
            findings.Add(Blocker(
                DeploymentCodes.WatchdogNotReady,
                "Watchdog deadline/startup schedulers are not present for commit readiness.",
                "scheduler",
                requiresRollback: true));
        }
        else if (!deadlineSchedulerEnabled)
        {
            findings.Add(Blocker(
                DeploymentCodes.WatchdogNotReady,
                "Watchdog deadline scheduler is disabled before commit.",
                "deadline",
                requiresRollback: true));
        }

        return new ManagedIntegrityResult { Findings = findings };
    }

    /// <summary>
    /// Canonical hash of observed managed filter jump-targets (anchors) for integrity evidence.
    /// Full artifact resource_hash remains the plan's NewArtifactHash; this covers AC#2 projection.
    /// </summary>
    public static Hash256 HashObservedAnchorTargets(IReadOnlyDictionary<string, string> jumpByMarker)
    {
        ArgumentNullException.ThrowIfNull(jumpByMarker);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        AppendUInt32Be(hasher, (uint)jumpByMarker.Count);
        foreach ((string marker, string jump) in jumpByMarker.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, marker);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, jump.Trim());
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static DeploymentVerificationFinding Blocker(
        string code,
        string message,
        string? target,
        bool requiresRollback)
        => new()
        {
            Code = code,
            Severity = DeploymentCodes.SeverityBlocker,
            Message = message,
            Target = target,
            RequiresRollback = requiresRollback,
        };

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendUInt32Be(IncrementalHash hasher, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        hasher.AppendData(buf);
    }
}
