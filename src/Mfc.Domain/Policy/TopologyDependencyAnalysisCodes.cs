namespace Mfc.Domain.Policy;

/// <summary>Frozen topology and dependency safety codes (Policy Model §47–§53 / M2-14).</summary>
public static class TopologyDependencyAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    public const string SeverityWarning = "WARNING";

    /// <summary>A declared VRRP member has no matching family+VRID+interface observation.</summary>
    public const string VrrpMemberMissing = "VRRP_MEMBER_MISSING";

    /// <summary>An uplink has no zone binding (Policy Model §48.1).</summary>
    public const string UplinkZoneCoverageMissing = "UPLINK_ZONE_COVERAGE_MISSING";

    public const string StrictRpfWithRoutingTables = "STRICT_RPF_WITH_ROUTING_TABLES";

    public const string StrictRpfWithVrrp = "STRICT_RPF_WITH_VRRP";

    public const string StrictRpfWithAsymmetricRouting = "STRICT_RPF_WITH_ASYMMETRIC_ROUTING";

    public const string InvalidDropWithAsymmetricRouting = "INVALID_DROP_WITH_ASYMMETRIC_ROUTING";

    public const string RawNotrackIntersectsStateful = "RAW_NOTRACK_INTERSECTS_STATEFUL_RULE";

    public const string RawNotrackTrafficNotHandled = "RAW_NOTRACK_TRAFFIC_NOT_HANDLED";

    public const string RawDependencyIndeterminate = "RAW_DEPENDENCY_INDETERMINATE";

    /// <summary>Candidate uses DSTNAT matchers but no dstnat NAT evidence exists (warning).</summary>
    public const string DstNatMatchWithoutNatEvidence = "DSTNAT_MATCH_WITHOUT_NAT_EVIDENCE";

    public const string NatDependencyChanged = "NAT_DEPENDENCY_CHANGED";

    public const string NatDependencyIndeterminate = "NAT_DEPENDENCY_INDETERMINATE";

    /// <summary>PCC matcher present — detection only; does not block ordinary filter (M2-15 FastTrack).</summary>
    public const string ManglePccPresent = "MANGLE_PCC_PRESENT";

    /// <summary>Routing-mark generation or lookup present — detection only.</summary>
    public const string MangleRoutingMarkPresent = "MANGLE_ROUTING_MARK_PRESENT";

    public const string MangleDependencyChanged = "MANGLE_DEPENDENCY_CHANGED";

    public const string MangleAnalysisIndeterminate = "MANGLE_ANALYSIS_INDETERMINATE";

    public const string SwitchForwardPolicyUnsupported = "SWITCH_FORWARD_POLICY_UNSUPPORTED";

    public const string SwitchHardwareProfileUnknown = "SWITCH_HARDWARE_PROFILE_UNKNOWN";

    public const string SwitchTransitPathNotProven = "SWITCH_TRANSIT_PATH_NOT_PROVEN";

    /// <summary>
    /// Topology-dependency BLOCKERs that must map to FailedPrecondition.
    /// PCC / routing-mark presence and DSTNAT-without-evidence are warnings.
    /// </summary>
    public static bool IsFailedPrecondition(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        if (string.Equals(code, ManglePccPresent, StringComparison.Ordinal)
            || string.Equals(code, MangleRoutingMarkPresent, StringComparison.Ordinal)
            || string.Equals(code, DstNatMatchWithoutNatEvidence, StringComparison.Ordinal))
        {
            return false;
        }

        return code.StartsWith("STRICT_RPF_", StringComparison.Ordinal)
               || code.StartsWith("INVALID_DROP_", StringComparison.Ordinal)
               || code.StartsWith("RAW_", StringComparison.Ordinal)
               || code.StartsWith("NAT_", StringComparison.Ordinal)
               || code.StartsWith("DSTNAT_", StringComparison.Ordinal)
               || code.StartsWith("SWITCH_", StringComparison.Ordinal)
               || code.StartsWith("VRRP_MEMBER_", StringComparison.Ordinal)
               || code.StartsWith("UPLINK_ZONE_", StringComparison.Ordinal)
               || string.Equals(code, MangleAnalysisIndeterminate, StringComparison.Ordinal)
               || string.Equals(code, MangleDependencyChanged, StringComparison.Ordinal);
    }
}
