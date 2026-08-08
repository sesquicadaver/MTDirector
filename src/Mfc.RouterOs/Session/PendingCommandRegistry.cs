using System.Diagnostics.CodeAnalysis;
using Mfc.RouterOs.Protocol;

namespace Mfc.RouterOs.Session;

internal sealed class PendingCommand
{
    public required ulong Tag { get; init; }

    public required string CommandId { get; init; }

    public RosCommandLifecycle Lifecycle { get; set; } = RosCommandLifecycle.Pending;

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset Deadline { get; init; }

    public List<RosSentence> Records { get; } = [];

    public List<RosTrap> Traps { get; } = [];

    public bool HadEmpty { get; set; }

    public int PayloadBytes { get; set; }

    public TaskCompletionSource<RosCommandResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>Bounded pending-command map (Spec §12.2) — never an unbounded ConcurrentDictionary.</summary>
internal sealed class PendingCommandRegistry
{
    private readonly Dictionary<ulong, PendingCommand> _byTag = [];
    private readonly object _gate = new();
    private readonly int _maxPending;

    public PendingCommandRegistry(int maxPending)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPending, 1);
        _maxPending = maxPending;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byTag.Count;
            }
        }
    }

    public bool TryAdd(PendingCommand command, [NotNullWhen(false)] out RouterOsProtocolError? error)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            if (_byTag.Count >= _maxPending)
            {
                error = new RouterOsProtocolError(
                    "API_PENDING_COMMANDS_EXCEEDED",
                    $"Pending command limit {_maxPending} exceeded.");
                return false;
            }

            if (!_byTag.TryAdd(command.Tag, command))
            {
                error = new RouterOsProtocolError(
                    "API_DUPLICATE_TAG",
                    $"Tag {command.Tag} is already pending.");
                return false;
            }

            error = null;
            return true;
        }
    }

    public bool TryGet(ulong tag, [NotNullWhen(true)] out PendingCommand? command)
    {
        lock (_gate)
        {
            return _byTag.TryGetValue(tag, out command);
        }
    }

    public bool TryRemove(ulong tag, [NotNullWhen(true)] out PendingCommand? command)
    {
        lock (_gate)
        {
            return _byTag.Remove(tag, out command);
        }
    }

    public PendingCommand[] Snapshot()
    {
        lock (_gate)
        {
            return _byTag.Values.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _byTag.Clear();
        }
    }
}
