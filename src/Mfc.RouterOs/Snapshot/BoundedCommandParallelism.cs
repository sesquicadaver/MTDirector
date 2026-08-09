namespace Mfc.RouterOs.Snapshot;

/// <summary>Bounded concurrency gate for parallel RouterOS section reads (max 8 per device).</summary>
public sealed class BoundedCommandParallelism : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate;

    public BoundedCommandParallelism(int maxParallelCommands)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParallelCommands, 1);
        MaxParallelCommands = maxParallelCommands;
        _gate = new SemaphoreSlim(maxParallelCommands, maxParallelCommands);
    }

    public int MaxParallelCommands { get; }

    /// <summary>Runs <paramref name="action"/> under the concurrency gate.</summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs independent section readers with bounded parallelism; preserves result order.</summary>
    public async Task<IReadOnlyList<T>> RunAllAsync<T>(
        IReadOnlyList<Func<CancellationToken, Task<T>>> actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Task<T>[] tasks = new Task<T>[actions.Count];
        for (int i = 0; i < actions.Count; i++)
        {
            Func<CancellationToken, Task<T>> action = actions[i];
            tasks[i] = RunAsync(action, cancellationToken);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
