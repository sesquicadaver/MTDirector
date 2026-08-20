using System.Diagnostics.CodeAnalysis;

namespace Mfc.Controller.Jobs;

/// <summary>
/// Fixed-capacity priority work queue. When full, <see cref="TryEnqueue"/> fails closed
/// (reject / drop-with-log at the caller) — never grows unbounded.
/// </summary>
public sealed class BoundedWorkBag<T>
    where T : class
{
    private readonly object _gate = new();
    private readonly List<T> _items;
    private readonly Comparison<T> _compare;
    private readonly int _capacity;

    public BoundedWorkBag(int capacity, Comparison<T> compare)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Queue capacity must be >= 1.");
        }

        ArgumentNullException.ThrowIfNull(compare);
        _capacity = capacity;
        _compare = compare;
        _items = new List<T>(capacity);
    }

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public bool IsFull => Count >= _capacity;

    /// <summary>Attempts to enqueue. Returns false when at capacity (fail-closed).</summary>
    public bool TryEnqueue(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            if (_items.Count >= _capacity)
            {
                return false;
            }

            _items.Add(item);
            _items.Sort(_compare);
            return true;
        }
    }

    /// <summary>Dequeues the highest-priority item, or returns false when empty.</summary>
    public bool TryDequeue([NotNullWhen(true)] out T? item)
    {
        lock (_gate)
        {
            if (_items.Count == 0)
            {
                item = null;
                return false;
            }

            item = _items[0];
            _items.RemoveAt(0);
            return true;
        }
    }

    /// <summary>Snapshot of kinds currently queued (tests / diagnostics).</summary>
    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
        {
            return _items.ToArray();
        }
    }
}

/// <summary>Factory for the operational job priority queue.</summary>
public static class OperationalJobQueues
{
    public static BoundedWorkBag<OperationalJobWorkItem> Create(int capacity)
        => new(
            capacity,
            static (a, b) =>
            {
                int byPriority = a.Priority.CompareTo(b.Priority);
                return byPriority != 0
                    ? byPriority
                    : a.EnqueuedAtUtc.CompareTo(b.EnqueuedAtUtc);
            });
}
