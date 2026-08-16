using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Caller-supplied finding captured in an immutable analysis run (M2-17).</summary>
public sealed class PolicyApprovalFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string Target { get; init; } = string.Empty;

    public required Hash256 WarningHash { get; init; }
}

/// <summary>Caller-supplied test outcome captured in an immutable analysis run (M2-17).</summary>
public sealed class PolicyApprovalTestOutcome
{
    public required PolicyTestId TestId { get; init; }

    public required string Origin { get; init; }

    public required string Outcome { get; init; }

    public required string Proof { get; init; }
}

/// <summary>
/// Dependency vector frozen inside the analysis run (Policy Model §64).
/// Runtime observations (VRRP role, active WAN) are intentionally absent.
/// </summary>
public sealed class PolicyApprovalDependencyVector
{
    public required Hash256 CompanyBindingHash { get; init; }

    public required Hash256 SiteBindingHash { get; init; }

    public required Hash256 NodeBindingHash { get; init; }

    public required Hash256 ActiveExceptionsHash { get; init; }

    public required Hash256 ZoneBindingHash { get; init; }

    public required Hash256 NodeMembershipHash { get; init; }

    public required Hash256 RouterOsConfigurationHash { get; init; }

    public required Hash256 CapabilityHash { get; init; }

    public required Hash256 CompatibilityHash { get; init; }

    public required Hash256 ManagementAccessProfileHash { get; init; }

    public required Hash256 AnchorGuardContextHash { get; init; }

    public required string AnalyzerVersion { get; init; }

    public required string PolicySchemaVersion { get; init; }

    public required string PipelineVersion { get; init; }
}

/// <summary>Pure-function result of <see cref="PolicyApprovalGate"/>.</summary>
public sealed class PolicyApprovalEvaluation
{
    public required string Outcome { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public required bool CompletesApproval { get; init; }

    public static PolicyApprovalEvaluation Reject(string code, string message)
        => new()
        {
            Outcome = PolicyApprovalCodes.OutcomeReject,
            ErrorCode = code,
            ErrorMessage = message,
            CompletesApproval = false,
        };

    public static PolicyApprovalEvaluation RecordVote()
        => new()
        {
            Outcome = PolicyApprovalCodes.OutcomeRecordVote,
            CompletesApproval = false,
        };

    public static PolicyApprovalEvaluation Approve()
        => new()
        {
            Outcome = PolicyApprovalCodes.OutcomeApprove,
            CompletesApproval = true,
        };
}

/// <summary>Pure-function result of <see cref="PolicyBindingGate"/>.</summary>
public sealed class PolicyBindingEvaluation
{
    public required bool Allowed { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PolicyBindingEvaluation Ok()
        => new() { Allowed = true };

    public static PolicyBindingEvaluation Reject(string code, string message)
        => new()
        {
            Allowed = false,
            ErrorCode = code,
            ErrorMessage = message,
        };
}
