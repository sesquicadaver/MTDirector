namespace Mfc.Controller.Jobs;

/// <summary>Five MVP operational job kinds (E2E §49). Lower ordinal = higher priority.</summary>
public enum OperationalJobKind : byte
{
    /// <summary>Highest priority — recover nonterminal ops after restart.</summary>
    OperationRecovery = 0,

    LockHeartbeat = 1,

    ExpiredExceptionReconciliation = 2,

    WatchdogResidueCleanup = 3,

    /// <summary>Lowest among MVP jobs — periodic drift capture.</summary>
    DriftCapture = 4,
}

/// <summary>Work item enqueued into the bounded priority queue.</summary>
public sealed class OperationalJobWorkItem
{
    public required OperationalJobKind Kind { get; init; }

    public required DateTimeOffset EnqueuedAtUtc { get; init; }

    /// <summary>Optional device id for cleanup / targeted work.</summary>
    public Guid? DeviceId { get; init; }

    /// <summary>Optional candidate resource names for watchdog residue cleanup.</summary>
    public IReadOnlyList<string> CandidateNames { get; init; } = [];

    public int Priority => (int)Kind;
}
