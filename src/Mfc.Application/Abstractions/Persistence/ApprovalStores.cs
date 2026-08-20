using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Abstractions.Persistence;

/// <summary>Append-only analysis/approval records and mutable desired bindings (M2-17).</summary>
public interface IPolicyApprovalStore
{
    Task AddAnalysisRunAsync(PolicyAnalysisRun run, CancellationToken cancellationToken = default);

    Task<PolicyAnalysisRun?> GetAnalysisRunAsync(
        PolicyAnalysisRunId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyAnalysisRun>> ListAnalysisRunsForRevisionAsync(
        PolicyRevisionId revisionId,
        CancellationToken cancellationToken = default);

    Task AddWarningAcknowledgmentAsync(
        PolicyWarningAcknowledgment acknowledgment,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyWarningAcknowledgment>> ListAcknowledgmentsAsync(
        PolicyAnalysisRunId analysisRunId,
        CancellationToken cancellationToken = default);

    Task AddApprovalAsync(PolicyApproval approval, CancellationToken cancellationToken = default);

    Task<PolicyApproval?> GetApprovalAsync(
        PolicyApprovalId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyApproval>> ListApprovalsAsync(
        PolicyRevisionId revisionId,
        CancellationToken cancellationToken = default);

    Task AddBindingAsync(PolicyDesiredBinding binding, CancellationToken cancellationToken = default);

    Task SaveBindingAsync(PolicyDesiredBinding binding, CancellationToken cancellationToken = default);

    Task<PolicyDesiredBinding?> GetBindingAsync(
        PolicyBindingId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyDesiredBinding>> ListActiveBindingsAsync(
        PolicyBindingScope scope,
        Guid? scopeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ACTIVE EXCEPTION bindings past valid_until (M6-03 expired-exception reconciliation).
    /// </summary>
    Task<IReadOnlyList<PolicyDesiredBinding>> ListDueExceptionBindingsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Runs store mutations in one database transaction.</summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}

/// <summary>Mapped unique/check conflict from persistence (cardinality, SoD).</summary>
public sealed class PersistenceConflictException : Exception
{
    public PersistenceConflictException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
