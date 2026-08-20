using System.Net;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Deployment;

/// <summary>Outcome of <see cref="VerifyMultiWanDeploymentUseCase"/> (M4-09).</summary>
public sealed class MultiWanDeploymentVerificationResult
{
    public required bool Succeeded { get; init; }

    public required bool RequiresRollback { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<DeploymentVerificationFinding> Findings { get; init; }

    public required int MultiWanProbeCount { get; init; }

    public required bool SkippedBecauseNotMultiWan { get; init; }
}

/// <summary>
/// Multi-WAN dependency recheck + topology-shaped probes (Safe Deployment Spec §36 / M4-09).
/// Does not mutate routing/NAT/Mangle and never forces WAN failover.
/// </summary>
public static class VerifyMultiWanDeploymentUseCase
{
    public static async Task<MultiWanDeploymentVerificationResult> ExecuteAsync(
        DeclaredUplinkMode uplinkMode,
        MultiWanDependencyHashes expectedDependencies,
        MultiWanDependencyHashes observedDependencies,
        MultiWanUplinkTopology topology,
        IReadOnlyList<DeploymentProbe> planProbes,
        IReadOnlyList<string> writePathTokens,
        Hash256 planArtifactHash,
        Hash256 activeRouteObservation,
        IRouterOsDeploymentSession? probeSession = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedDependencies);
        ArgumentNullException.ThrowIfNull(observedDependencies);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(planProbes);
        ArgumentNullException.ThrowIfNull(writePathTokens);
        ArgumentNullException.ThrowIfNull(planArtifactHash);
        ArgumentNullException.ThrowIfNull(activeRouteObservation);

        if (!MultiWanDeploymentVerification.RequiresMultiWanVerification(uplinkMode))
        {
            return new MultiWanDeploymentVerificationResult
            {
                Succeeded = true,
                RequiresRollback = false,
                Findings = [],
                MultiWanProbeCount = 0,
                SkippedBecauseNotMultiWan = true,
            };
        }

        List<DeploymentVerificationFinding> findings = [];

        // AC#3: sealed artifact ignores active-route observation.
        Hash256 ignored = MultiWanDeploymentVerification.ArtifactHashIgnoringActiveRoute(
            planArtifactHash,
            activeRouteObservation);
        if (!ignored.Equals(planArtifactHash))
        {
            findings.Add(new DeploymentVerificationFinding
            {
                Code = DeploymentCodes.ActiveArtifactHashMismatch,
                Severity = DeploymentCodes.SeverityBlocker,
                Message = "Active route observation must not alter the sealed artifact hash.",
                Target = "artifact",
                RequiresRollback = true,
            });
            return Fail(findings, probeCount: 0);
        }

        ManagedIntegrityResult surface = MultiWanDeploymentVerification.EnsureFilterOnlyWriteSurface(writePathTokens);
        findings.AddRange(surface.Findings);
        if (!surface.Passed)
        {
            return Fail(findings, probeCount: 0);
        }

        ManagedIntegrityResult deps = MultiWanDeploymentVerification.RecheckDependencyHashes(
            expectedDependencies,
            observedDependencies);
        findings.AddRange(deps.Findings);
        if (!deps.Passed)
        {
            return Fail(findings, probeCount: 0);
        }

        ManagedIntegrityResult planned = MultiWanDeploymentVerification.PlanRuntimeProbes(
            topology,
            planProbes,
            out IReadOnlyList<DeploymentProbe> selected);
        findings.AddRange(planned.Findings);
        if (!planned.Passed)
        {
            return Fail(findings, probeCount: 0);
        }

        int probeCount = 0;
        if (selected.Count > 0)
        {
            if (probeSession is null)
            {
                findings.Add(new DeploymentVerificationFinding
                {
                    Code = DeploymentCodes.DeploymentProbeFailed,
                    Severity = DeploymentCodes.SeverityBlocker,
                    Message = "Multi-WAN runtime probes require a deployment session.",
                    Target = "session",
                    RequiresRollback = true,
                });
                return Fail(findings, probeCount: 0);
            }

            foreach (DeploymentProbe probe in selected)
            {
                probeCount++;
                RouterPingResult ping = await probeSession.PingAsync(
                    ToPingRequest(probe),
                    cancellationToken).ConfigureAwait(false);
                DeploymentVerificationFinding? classified = PostActivationVerification.ClassifyCriticalProbeOutcome(
                    probe.Kind,
                    probe.Destination,
                    ping.Outcome.ToString());
                if (classified is not null)
                {
                    findings.Add(classified);
                    return Fail(findings, probeCount);
                }
            }
        }

        return new MultiWanDeploymentVerificationResult
        {
            Succeeded = true,
            RequiresRollback = false,
            Findings = findings,
            MultiWanProbeCount = probeCount,
            SkippedBecauseNotMultiWan = false,
        };
    }

    private static RouterPingRequest ToPingRequest(DeploymentProbe probe)
    {
        IPAddress destination = IPAddress.Parse(probe.Destination);
        IPAddress? source = probe.SourceAddress is null ? null : IPAddress.Parse(probe.SourceAddress);
        return new RouterPingRequest(
            destination,
            probe.Family,
            probe.TimeoutMilliseconds,
            source,
            probe.RoutingTable,
            probe.Interface);
    }

    private static MultiWanDeploymentVerificationResult Fail(
        List<DeploymentVerificationFinding> findings,
        int probeCount)
        => new()
        {
            Succeeded = false,
            RequiresRollback = findings.Any(static f => f.RequiresRollback),
            Code = findings.FirstOrDefault(static f => f.Severity == DeploymentCodes.SeverityBlocker)?.Code,
            Message = findings.FirstOrDefault(static f => f.Severity == DeploymentCodes.SeverityBlocker)?.Message,
            Findings = findings,
            MultiWanProbeCount = probeCount,
            SkippedBecauseNotMultiWan = false,
        };
}
