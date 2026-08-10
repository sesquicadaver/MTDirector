namespace Mfc.Domain.Policy;

/// <summary>Filter chain surface for Pipeline v1 (Policy Model §12).</summary>
public enum PolicyFilterChain : byte
{
    Input = 0,
    Forward = 1,
    Output = 2,
}

/// <summary>Fixed logical pipeline stages in normative order (Policy Model §12).</summary>
public enum PolicyPipelineStage : byte
{
    ProtectedControlPlane = 0,
    MandatoryPreStateDeny = 1,
    StatePrelude = 2,
    CompanyDenyExemptions = 3,
    CompanyDeny = 4,
    SiteDenyExemptions = 5,
    SiteDeny = 6,
    NodeDenyExemptions = 7,
    NodeDeny = 8,
    CompanyAllow = 9,
    SiteAllow = 10,
    NodeAllow = 11,
    DefaultDisposition = 12,
}

/// <summary>Rule effect kinds allowed in Pipeline v1 stages (Policy Model §13 / §26).</summary>
public enum PolicyRuleEffect : byte
{
    Accept = 0,
    Drop = 1,
    Reject = 2,
    FasttrackAccept = 3,
    ExemptDenyStage = 4,
}

/// <summary>Company-baseline default disposition for a family/chain (Policy Model §15).</summary>
public enum ChainDefaultDisposition : byte
{
    Drop = 0,
    Reject = 1,
    ReturnToUnmanaged = 2,
}

/// <summary>Reject mode when default disposition or rule effect is REJECT (Policy Model §26).</summary>
public enum RejectMode : byte
{
    TcpReset = 0,
    AdminProhibited = 1,
    PortUnreachable = 2,
}

/// <summary>
/// Runtime deployment mode affecting chain-contract permissions (Policy Model §15 rule 5).
/// </summary>
public enum PolicyRuntimeMode : byte
{
    /// <summary>Fully managed; RETURN_TO_UNMANAGED is forbidden.</summary>
    ManagedOnly = 0,

    /// <summary>Migration / coexistence; RETURN_TO_UNMANAGED is allowed with CRITICAL risk.</summary>
    MigrationCoexistence = 1,
}
