using Mfc.RouterOs.Protocol;

namespace Mfc.RouterOs.Session;

/// <summary>Lifecycle of a pending tagged command.</summary>
public enum RosCommandLifecycle : byte
{
    Pending = 0,
    CancelRequested = 1,
    LimitExceeded = 2,
    Completed = 3,
    Faulted = 4,
    TimedOut = 5,
    Cancelled = 6,
}

/// <summary>Sanitized trap captured before <c>!done</c>.</summary>
public sealed class RosTrap
{
    public required IReadOnlyList<RosAttributeEntry> Attributes { get; init; }
}

/// <summary>Completed tagged command result.</summary>
public sealed class RosCommandResult
{
    public required ulong Tag { get; init; }

    public required RosCommandLifecycle Lifecycle { get; init; }

    public required IReadOnlyList<RosSentence> Records { get; init; }

    public required IReadOnlyList<RosTrap> Traps { get; init; }

    public bool HadEmpty { get; init; }

    public RouterOsProtocolError? Error { get; init; }
}
