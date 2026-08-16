namespace Mfc.Domain.Policy;

/// <summary>Policy document kind (Policy Model §7).</summary>
public enum PolicyKind : byte
{
    CompanyBaseline = 0,
    SiteOverlay = 1,
    NodeOverlay = 2,
    Exception = 3,
}

/// <summary>Owner scope for a policy container (Policy Model §7).</summary>
public enum PolicyOwnerScope : byte
{
    Company = 0,
    Site = 1,
    Node = 2,
}

/// <summary>Policy container status (Policy Model §7).</summary>
public enum PolicyStatus : byte
{
    Active = 0,
    Archived = 1,
}

/// <summary>Policy revision lifecycle state (Policy Model §8–§9).</summary>
public enum PolicyRevisionState : byte
{
    Draft = 0,
    Validated = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Superseded = 5,
    Revoked = 6,
}

/// <summary>Desired-binding scope (Policy Model §10).</summary>
public enum PolicyBindingScope : byte
{
    Company = 0,
    Site = 1,
    Node = 2,
    Exception = 3,
}

/// <summary>Desired-binding state (Policy Model §10). Expiry never deploys.</summary>
public enum PolicyBindingState : byte
{
    Active = 0,
    Disabled = 1,
    ExpiredPendingReconciliation = 2,
}
