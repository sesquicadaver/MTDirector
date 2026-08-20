using System.Net;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Result of post-activation verification (Safe Deployment Spec §32–§34 / M4-07).</summary>
public sealed class DeploymentVerificationResult
{
    public required bool Succeeded { get; init; }

    public required bool RequiresRollback { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<DeploymentVerificationFinding> Findings { get; init; }

    public required bool UsedFreshApiSslSession { get; init; }

    public required int ProbeCount { get; init; }
}

/// <summary>
/// Opens an independent API-SSL management session for post-activation verification (AC#3 / AC#4).
/// Must not reuse the activation/staging session object.
/// </summary>
public interface IDeploymentFreshSessionFactory
{
    Task<IRouterOsDeploymentSession> OpenFreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies active anchors, managed resource hash, fresh API-SSL connectivity, bounded probes,
/// and watchdog readiness before commit (M4-07).
/// </summary>
public static class VerifyDeploymentActivationUseCase
{
    public static async Task<DeploymentVerificationResult> ExecuteAsync(
        DeviceDeploymentPlan devicePlan,
        object? priorSessionIdentity,
        IDeploymentFreshSessionFactory freshSessionFactory,
        Hash256 observedManagedResourceHash,
        DeploymentWatchdogBundle armedWatchdog,
        TimeSpan remainingWatchdogTtl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(freshSessionFactory);
        ArgumentNullException.ThrowIfNull(observedManagedResourceHash);
        ArgumentNullException.ThrowIfNull(armedWatchdog);

        List<DeploymentVerificationFinding> findings = [];
        ManagedIntegrityResult profile = PostActivationVerification.ValidateProbeProfile(devicePlan.Probes);
        findings.AddRange(profile.Findings);
        if (!profile.Passed)
        {
            return Fail(findings, usedFresh: false, probeCount: 0);
        }

        ManagedIntegrityResult hashCheck = PostActivationVerification.VerifyManagedResourceHash(
            devicePlan.NewArtifactHash,
            observedManagedResourceHash);
        findings.AddRange(hashCheck.Findings);
        if (!hashCheck.Passed)
        {
            return Fail(findings, usedFresh: false, probeCount: 0);
        }

        IRouterOsDeploymentSession fresh;
        try
        {
            fresh = await freshSessionFactory.OpenFreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Domain.DomainInvariantException or IOException)
        {
            findings.Add(new DeploymentVerificationFinding
            {
                Code = DeploymentCodes.ManagementReconnectFailed,
                Severity = DeploymentCodes.SeverityBlocker,
                Message = "Failed to open a fresh API-SSL management session.",
                Target = "api-ssl",
                RequiresRollback = true,
            });
            return Fail(findings, usedFresh: false, probeCount: 0);
        }

        await using (fresh.ConfigureAwait(false))
        {
            if (priorSessionIdentity is not null && ReferenceEquals(priorSessionIdentity, fresh))
            {
                findings.Add(new DeploymentVerificationFinding
                {
                    Code = DeploymentCodes.ManagementReconnectFailed,
                    Severity = DeploymentCodes.SeverityBlocker,
                    Message = "Fresh API-SSL session must not reuse the established activation session.",
                    Target = "api-ssl",
                    RequiresRollback = true,
                });
                return Fail(findings, usedFresh: false, probeCount: 0);
            }

            ActualManagedState state;
            try
            {
                state = await fresh.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                findings.Add(new DeploymentVerificationFinding
                {
                    Code = DeploymentCodes.ManagementReconnectFailed,
                    Severity = DeploymentCodes.SeverityBlocker,
                    Message = "Fresh API-SSL session could not read managed state.",
                    Target = "api-ssl",
                    RequiresRollback = true,
                });
                return Fail(findings, usedFresh: true, probeCount: 0);
            }

            Dictionary<string, string> jumpByMarker = ExtractAnchorJumps(state);
            ManagedIntegrityResult anchors = PostActivationVerification.VerifyActiveAnchors(
                devicePlan.NewAnchorTargets,
                jumpByMarker);
            findings.AddRange(anchors.Findings);
            if (!anchors.Passed)
            {
                return Fail(findings, usedFresh: true, probeCount: 0);
            }

            int probeCount = 0;
            foreach (DeploymentProbe probe in devicePlan.Probes)
            {
                probeCount++;
                if (probe.Kind == DeploymentProbeKind.ApiSsl)
                {
                    // Opening + reading on the fresh session is the API_SSL probe (Spec §33.1).
                    // Destination is the management address identity recorded on the plan.
                    continue;
                }

                RouterPingResult ping = await fresh.PingAsync(
                    ToPingRequest(probe),
                    cancellationToken).ConfigureAwait(false);
                DeploymentVerificationFinding? classified = PostActivationVerification.ClassifyCriticalProbeOutcome(
                    probe.Kind,
                    probe.Destination,
                    ping.Outcome.ToString());
                if (classified is not null)
                {
                    findings.Add(classified);
                    return Fail(findings, usedFresh: true, probeCount);
                }
            }

            bool deadlinePresent = state.Schedulers.Any(s =>
                string.Equals(s.GetValueOrDefault("name"), armedWatchdog.DeadlineSchedulerName, StringComparison.Ordinal));
            bool startupPresent = state.Schedulers.Any(s =>
                string.Equals(s.GetValueOrDefault("name"), armedWatchdog.StartupSchedulerName, StringComparison.Ordinal));
            bool deadlineEnabled = state.Schedulers.Any(s =>
                string.Equals(s.GetValueOrDefault("name"), armedWatchdog.DeadlineSchedulerName, StringComparison.Ordinal)
                && IsEnabled(s.GetValueOrDefault("disabled")));

            ManagedIntegrityResult watchdog = PostActivationVerification.VerifyWatchdogReadiness(
                remainingWatchdogTtl,
                deadlinePresent,
                deadlineEnabled,
                startupPresent);
            findings.AddRange(watchdog.Findings);
            if (!watchdog.Passed)
            {
                return Fail(findings, usedFresh: true, probeCount);
            }

            return new DeploymentVerificationResult
            {
                Succeeded = true,
                RequiresRollback = false,
                Findings = findings,
                UsedFreshApiSslSession = true,
                ProbeCount = probeCount,
            };
        }
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

    private static Dictionary<string, string> ExtractAnchorJumps(ActualManagedState state)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (IReadOnlyDictionary<string, string> row in state.Ipv4FilterRules.Concat(state.Ipv6FilterRules))
        {
            string? comment = row.GetValueOrDefault("comment");
            if (string.IsNullOrWhiteSpace(comment)
                || !comment.StartsWith("mfc:anchor:", StringComparison.Ordinal)
                || !string.Equals(row.GetValueOrDefault("action"), "jump", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? jump = row.GetValueOrDefault("jump-target");
            if (!string.IsNullOrWhiteSpace(jump))
            {
                map[comment] = jump.Trim();
            }
        }

        return map;
    }

    private static bool IsEnabled(string? disabledFlag)
        => disabledFlag is null
           || disabledFlag.Equals("false", StringComparison.OrdinalIgnoreCase)
           || disabledFlag.Equals("no", StringComparison.OrdinalIgnoreCase);

    private static DeploymentVerificationResult Fail(
        List<DeploymentVerificationFinding> findings,
        bool usedFresh,
        int probeCount)
        => new()
        {
            Succeeded = false,
            RequiresRollback = findings.Any(static f => f.RequiresRollback),
            Code = findings.FirstOrDefault(static f => f.Severity == DeploymentCodes.SeverityBlocker)?.Code,
            Message = findings.FirstOrDefault(static f => f.Severity == DeploymentCodes.SeverityBlocker)?.Message,
            Findings = findings,
            UsedFreshApiSslSession = usedFresh,
            ProbeCount = probeCount,
        };
}
