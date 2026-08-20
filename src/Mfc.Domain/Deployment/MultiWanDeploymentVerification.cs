using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Sealed multi-WAN dependency hashes re-checked before/after activation (Safe Deployment Spec §36 / M4-09).
/// </summary>
public sealed record MultiWanDependencyHashes
{
    public required Hash256 RoutingConfigHash { get; init; }

    public required Hash256 RoutingRuleHash { get; init; }

    public required Hash256 NatHash { get; init; }

    public required Hash256 RawHash { get; init; }

    public required Hash256 MangleHash { get; init; }

    public required Hash256 ZoneResolutionHash { get; init; }

    public required Hash256 InterfaceListMembershipHash { get; init; }

    public required Hash256 RpFilterHash { get; init; }

    public IEnumerable<(string Name, Hash256 Value)> Enumerate()
    {
        yield return ("routing", RoutingConfigHash);
        yield return ("routing-rule", RoutingRuleHash);
        yield return ("nat", NatHash);
        yield return ("raw", RawHash);
        yield return ("mangle", MangleHash);
        yield return ("zone", ZoneResolutionHash);
        yield return ("interface-list", InterfaceListMembershipHash);
        yield return ("rp-filter", RpFilterHash);
    }
}

/// <summary>Runtime uplink topology facts for multi-WAN probe planning (Spec §36.1).</summary>
public sealed record MultiWanUplinkTopology
{
    public required DeclaredUplinkMode UplinkMode { get; init; }

    /// <summary>Required routing-table names for BALANCED/MIXED per-table probes (empty for single-table failover).</summary>
    public required IReadOnlyList<string> RequiredRoutingTables { get; init; }

    /// <summary>Literal IP for the current active-path ROUTER_PING on single-table failover.</summary>
    public string? ActivePathDestination { get; init; }

    /// <summary>Must stay false — Controller never forces WAN failover (AC#8 / Spec §36.1).</summary>
    public required bool ForcedFailoverRequested { get; init; }

    /// <summary>Must stay false — Controller never disables primary WAN (AC#6).</summary>
    public required bool DisablePrimaryWanRequested { get; init; }

    /// <summary>Must stay false — Controller never invents a temporary route (AC#7).</summary>
    public required bool TemporaryRouteRequested { get; init; }
}

/// <summary>Pure multi-WAN deployment verification gates (Safe Deployment Spec §36 / M4-09 AC 1–10).</summary>
public static class MultiWanDeploymentVerification
{
    public const string AnalyzerVersion = "mfc.deployment.multiwan.v1";

    /// <summary>True when the Node needs multi-WAN deployment verification extras.</summary>
    public static bool RequiresMultiWanVerification(DeclaredUplinkMode mode)
        => mode is DeclaredUplinkMode.Failover or DeclaredUplinkMode.Balanced or DeclaredUplinkMode.Mixed;

    /// <summary>
    /// Active route observation must not be folded into the sealed filter artifact hash (AC#3 / Spec §36.2).
    /// Returns the plan artifact unchanged — route observation is intentionally ignored as an input.
    /// </summary>
    public static Hash256 ArtifactHashIgnoringActiveRoute(
        Hash256 planArtifactHash,
        Hash256 activeRouteObservation)
    {
        ArgumentNullException.ThrowIfNull(planArtifactHash);
        ArgumentNullException.ThrowIfNull(activeRouteObservation);
        // AC#3: current active WAN / route observation must not mutate the sealed artifact identity.
        _ = activeRouteObservation;
        return planArtifactHash;
    }

    /// <summary>Re-check routing/NAT/RAW/Mangle/zone/interface-list/rp-filter hashes (AC#1 / AC#2 / AC#9).</summary>
    public static ManagedIntegrityResult RecheckDependencyHashes(
        MultiWanDependencyHashes expected,
        MultiWanDependencyHashes observed)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        List<DeploymentVerificationFinding> findings = [];
        Dictionary<string, Hash256> got = observed.Enumerate()
            .ToDictionary(static e => e.Name, static e => e.Value, StringComparer.Ordinal);
        foreach ((string name, Hash256 want) in expected.Enumerate())
        {
            if (!got.TryGetValue(name, out Hash256? observedHash)
                || observedHash is null
                || !want.Equals(observedHash))
            {
                findings.Add(new DeploymentVerificationFinding
                {
                    Code = DeploymentCodes.MultiWanDependencyDrift,
                    Severity = DeploymentCodes.SeverityBlocker,
                    Message = $"Multi-WAN dependency '{name}' hash drifted since plan seal.",
                    Target = name,
                    RequiresRollback = true,
                });
            }
        }

        return new ManagedIntegrityResult { Findings = findings };
    }

    /// <summary>
    /// Reject forced failover / primary disable / temporary route intents (AC#6–#8).
    /// </summary>
    public static ManagedIntegrityResult RejectForbiddenOperationalIntents(MultiWanUplinkTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        List<DeploymentVerificationFinding> findings = [];
        if (topology.ForcedFailoverRequested)
        {
            findings.Add(Forbidden(
                DeploymentCodes.MultiWanForcedFailoverForbidden,
                "Forced WAN failover is not part of production deployment verification.",
                "forced-failover"));
        }

        if (topology.DisablePrimaryWanRequested)
        {
            findings.Add(Forbidden(
                DeploymentCodes.MultiWanForcedFailoverForbidden,
                "Controller must not disable the primary WAN during deployment.",
                "disable-primary"));
        }

        if (topology.TemporaryRouteRequested)
        {
            findings.Add(Forbidden(
                DeploymentCodes.MultiWanForcedFailoverForbidden,
                "Controller must not create a temporary route during deployment.",
                "temporary-route"));
        }

        return new ManagedIntegrityResult { Findings = findings };
    }

    /// <summary>
    /// Select ROUTER_PING probes required for the topology (AC#4 / AC#5 / AC#8).
    /// Balanced/Mixed with tables → one ping per table; failover → current active path only.
    /// </summary>
    public static ManagedIntegrityResult PlanRuntimeProbes(
        MultiWanUplinkTopology topology,
        IReadOnlyList<DeploymentProbe> planProbes,
        out IReadOnlyList<DeploymentProbe> selected)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(planProbes);
        selected = [];
        ManagedIntegrityResult forbidden = RejectForbiddenOperationalIntents(topology);
        if (!forbidden.Passed)
        {
            return forbidden;
        }

        List<DeploymentVerificationFinding> findings = [];
        List<DeploymentProbe> picks = [];

        bool perTable = topology.UplinkMode is DeclaredUplinkMode.Balanced or DeclaredUplinkMode.Mixed
                        && topology.RequiredRoutingTables.Count > 0;
        bool failoverActivePath = topology.UplinkMode is DeclaredUplinkMode.Failover
                                  || (topology.UplinkMode == DeclaredUplinkMode.Mixed && !perTable);

        if (topology.UplinkMode == DeclaredUplinkMode.Balanced && topology.RequiredRoutingTables.Count == 0)
        {
            findings.Add(new DeploymentVerificationFinding
            {
                Code = DeploymentCodes.MultiWanProbeCoverageMissing,
                Severity = DeploymentCodes.SeverityBlocker,
                Message = "Balanced topology requires at least one routing table for per-table probes.",
                Target = "routing-tables",
                RequiresRollback = true,
            });
        }
        else if (perTable)
        {
            foreach (string table in topology.RequiredRoutingTables.OrderBy(static t => t, StringComparer.Ordinal))
            {
                DeploymentProbe? match = planProbes.FirstOrDefault(p =>
                    p.Kind == DeploymentProbeKind.RouterPing
                    && string.Equals(p.RoutingTable, table, StringComparison.Ordinal));
                if (match is null)
                {
                    findings.Add(new DeploymentVerificationFinding
                    {
                        Code = DeploymentCodes.MultiWanProbeCoverageMissing,
                        Severity = DeploymentCodes.SeverityBlocker,
                        Message = $"Balanced/Mixed topology requires a ROUTER_PING probe for routing table '{table}'.",
                        Target = table,
                        RequiresRollback = true,
                    });
                    continue;
                }

                picks.Add(match);
            }
        }
        else if (failoverActivePath)
        {
            string? dest = topology.ActivePathDestination;
            if (string.IsNullOrWhiteSpace(dest))
            {
                findings.Add(new DeploymentVerificationFinding
                {
                    Code = DeploymentCodes.MultiWanProbeCoverageMissing,
                    Severity = DeploymentCodes.SeverityBlocker,
                    Message = "Failover topology requires a current active-path probe destination.",
                    Target = "active-path",
                    RequiresRollback = true,
                });
            }
            else
            {
                DeploymentProbe? match = planProbes.FirstOrDefault(p =>
                    p.Kind == DeploymentProbeKind.RouterPing
                    && string.Equals(p.Destination, dest.Trim(), StringComparison.Ordinal));
                if (match is null)
                {
                    findings.Add(new DeploymentVerificationFinding
                    {
                        Code = DeploymentCodes.MultiWanProbeCoverageMissing,
                        Severity = DeploymentCodes.SeverityBlocker,
                        Message = "Failover active-path ROUTER_PING is missing from the plan probe profile.",
                        Target = dest,
                        RequiresRollback = true,
                    });
                }
                else
                {
                    picks.Add(match);
                }
            }
        }

        selected = picks;
        return new ManagedIntegrityResult { Findings = findings };
    }

    /// <summary>
    /// Deployment write surface must stay filter/watchdog-only — no routing/NAT/Mangle/WAN edits (AC#6 / AC#7 / AC#10).
    /// </summary>
    public static ManagedIntegrityResult EnsureFilterOnlyWriteSurface(IReadOnlyList<string> writePathTokens)
    {
        ArgumentNullException.ThrowIfNull(writePathTokens);
        string[] forbidden =
        [
            "/ip/route",
            "/ipv6/route",
            "/routing",
            "/ip/firewall/nat",
            "/ipv6/firewall/nat",
            "/ip/firewall/raw",
            "/ipv6/firewall/raw",
            "/ip/firewall/mangle",
            "/ipv6/firewall/mangle",
            "/interface/disable",
            "/interface/set",
        ];
        List<DeploymentVerificationFinding> findings = [];
        foreach (string path in writePathTokens)
        {
            string normalized = path.Trim().ToLowerInvariant();
            if (forbidden.Any(f => normalized.StartsWith(f, StringComparison.Ordinal)
                                   || normalized.Contains(f, StringComparison.Ordinal)))
            {
                findings.Add(new DeploymentVerificationFinding
                {
                    Code = DeploymentCodes.MultiWanWriteSurfaceViolation,
                    Severity = DeploymentCodes.SeverityBlocker,
                    Message = "Multi-WAN deployment must not mutate routing/NAT/RAW/Mangle or disable WAN interfaces.",
                    Target = path,
                    RequiresRollback = false,
                });
            }
        }

        return new ManagedIntegrityResult { Findings = findings };
    }

    private static DeploymentVerificationFinding Forbidden(string code, string message, string target)
        => new()
        {
            Code = code,
            Severity = DeploymentCodes.SeverityBlocker,
            Message = message,
            Target = target,
            RequiresRollback = false,
        };
}
