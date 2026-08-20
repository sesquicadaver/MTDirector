using Mfc.Application.Jobs;
using Microsoft.Extensions.Options;

namespace Mfc.Controller.Jobs;

/// <summary>
/// In-process operational job scheduler (IHostedService / BackgroundService).
/// No Hangfire/Quartz/Rabbit/Kafka — bounded queue + cooperative cancellation only.
/// </summary>
public sealed partial class OperationalJobSchedulerHostedService : BackgroundService
{
    private readonly BoundedWorkBag<OperationalJobWorkItem> _queue;
    private readonly OperationalJobTickPlanner _planner;
    private readonly OperationalJobExecutor _executor;
    private readonly IOptionsMonitor<OperationalJobsOptions> _options;
    private readonly ILogger<OperationalJobSchedulerHostedService> _logger;
    private readonly SemaphoreSlim _captureGate;
    private readonly SemaphoreSlim _writeGate;
    private readonly object _scheduleGate = new();

    private DateTimeOffset? _lastRecoveryUtc;
    private DateTimeOffset? _lastHeartbeatUtc;
    private DateTimeOffset? _lastExpiredUtc;
    private DateTimeOffset? _lastCleanupUtc;
    private DateTimeOffset? _lastDriftUtc;

    /// <summary>Test hook: replace delay (default Task.Delay).</summary>
    public Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; }
        = static (delay, ct) => Task.Delay(delay, ct);

    /// <summary>Test hook: current UTC clock.</summary>
    public Func<DateTimeOffset> UtcNow { get; set; } = static () => DateTimeOffset.UtcNow;

    /// <summary>Optional cleanup candidates supplier (device → disabled temporary names).</summary>
    public Func<CancellationToken, Task<IReadOnlyList<(Guid DeviceId, IReadOnlyList<string> Names)>>>?
        CleanupCandidatesAsync
    { get; set; }

    public OperationalJobSchedulerHostedService(
        BoundedWorkBag<OperationalJobWorkItem> queue,
        OperationalJobTickPlanner planner,
        OperationalJobExecutor executor,
        IOptionsMonitor<OperationalJobsOptions> options,
        ILogger<OperationalJobSchedulerHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _queue = queue;
        _planner = planner;
        _executor = executor;
        _options = options;
        _logger = logger;
        OperationalJobsOptions opts = options.CurrentValue;
        _captureGate = new SemaphoreSlim(opts.MaxCaptureConcurrency, opts.MaxCaptureConcurrency);
        _writeGate = new SemaphoreSlim(opts.MaxWriteConcurrency, opts.MaxWriteConcurrency);
    }

    /// <summary>Exposes capture concurrency for Living Spec assertions.</summary>
    public int CaptureConcurrencyLimit => _options.CurrentValue.MaxCaptureConcurrency;

    /// <summary>Exposes write concurrency for Living Spec assertions.</summary>
    public int WriteConcurrencyLimit => _options.CurrentValue.MaxWriteConcurrency;

    public BoundedWorkBag<OperationalJobWorkItem> Queue => _queue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OperationalJobsOptions options = _options.CurrentValue;
        if (!options.Enabled)
        {
            try
            {
                await DelayAsync(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }

            return;
        }

        Task worker = RunWorkersAsync(stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await EnqueueDueAsync(stoppingToken).ConfigureAwait(false);
                await DelayAsync(TimeSpan.FromSeconds(options.SchedulerIdleSeconds), stoppingToken)
                    .ConfigureAwait(false);
                options = _options.CurrentValue;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        finally
        {
            try
            {
                await worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogStopping(_logger);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        LogStopped(_logger);
    }

    /// <summary>One planner tick — used by Living Spec without sleeping.</summary>
    public async Task EnqueueDueAsync(CancellationToken cancellationToken = default)
    {
        OperationalJobsOptions options = _options.CurrentValue;
        DateTimeOffset now = UtcNow().ToUniversalTime();
        IReadOnlyList<(Guid DeviceId, IReadOnlyList<string> CandidateNames)>? cleanup = null;
        if (CleanupCandidatesAsync is not null)
        {
            IReadOnlyList<(Guid DeviceId, IReadOnlyList<string> Names)> raw =
                await CleanupCandidatesAsync(cancellationToken).ConfigureAwait(false);
            cleanup = raw.Select(static t => (t.DeviceId, t.Names)).ToArray();
        }

        DateTimeOffset? lastRecovery;
        DateTimeOffset? lastHeartbeat;
        DateTimeOffset? lastExpired;
        DateTimeOffset? lastCleanup;
        DateTimeOffset? lastDrift;
        lock (_scheduleGate)
        {
            lastRecovery = _lastRecoveryUtc;
            lastHeartbeat = _lastHeartbeatUtc;
            lastExpired = _lastExpiredUtc;
            lastCleanup = _lastCleanupUtc;
            lastDrift = _lastDriftUtc;
        }

        IReadOnlyList<OperationalJobWorkItem> due = _planner.Plan(
            options,
            now,
            lastRecovery,
            lastHeartbeat,
            lastExpired,
            lastCleanup,
            lastDrift,
            cleanup);

        foreach (OperationalJobWorkItem item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_queue.TryEnqueue(item))
            {
                LogQueueFull(_logger, item.Kind.ToString(), _queue.Capacity);
                continue;
            }

            MarkScheduled(item.Kind, now);
        }
    }

    private void MarkScheduled(OperationalJobKind kind, DateTimeOffset now)
    {
        lock (_scheduleGate)
        {
            switch (kind)
            {
                case OperationalJobKind.OperationRecovery:
                    _lastRecoveryUtc = now;
                    break;
                case OperationalJobKind.LockHeartbeat:
                    _lastHeartbeatUtc = now;
                    break;
                case OperationalJobKind.ExpiredExceptionReconciliation:
                    _lastExpiredUtc = now;
                    break;
                case OperationalJobKind.WatchdogResidueCleanup:
                    _lastCleanupUtc = now;
                    break;
                case OperationalJobKind.DriftCapture:
                    _lastDriftUtc = now;
                    break;
            }
        }
    }

    private async Task RunWorkersAsync(CancellationToken stoppingToken)
    {
        Task[] workers =
        [
            DrainLoopAsync(stoppingToken),
            DrainLoopAsync(stoppingToken),
            DrainLoopAsync(stoppingToken),
            DrainLoopAsync(stoppingToken),
        ];
        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task DrainLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_queue.TryDequeue(out OperationalJobWorkItem? item))
            {
                try
                {
                    await DelayAsync(TimeSpan.FromMilliseconds(50), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                await ExecuteWithGatesAsync(item, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogJobFailed(_logger, item.Kind.ToString(), ex.Message);
            }
        }
    }

    private async Task ExecuteWithGatesAsync(OperationalJobWorkItem item, CancellationToken stoppingToken)
    {
        bool needsCapture = item.Kind is OperationalJobKind.DriftCapture or OperationalJobKind.OperationRecovery;
        bool needsWrite = item.Kind is OperationalJobKind.WatchdogResidueCleanup
            or OperationalJobKind.OperationRecovery;

        if (needsCapture)
        {
            await _captureGate.WaitAsync(stoppingToken).ConfigureAwait(false);
        }

        if (needsWrite)
        {
            await _writeGate.WaitAsync(stoppingToken).ConfigureAwait(false);
        }

        try
        {
            await _executor.ExecuteAsync(item, stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            if (needsWrite)
            {
                _writeGate.Release();
            }

            if (needsCapture)
            {
                _captureGate.Release();
            }
        }
    }

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Warning,
        Message = "Operational job queue full (capacity={Capacity}); rejected {Kind} (fail-closed).")]
    private static partial void LogQueueFull(ILogger logger, string kind, int capacity);

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Error,
        Message = "Operational job {Kind} failed: {Error}")]
    private static partial void LogJobFailed(ILogger logger, string kind, string error);

    [LoggerMessage(
        EventId = 6103,
        Level = LogLevel.Information,
        Message = "Operational job scheduler stopping; cancelling in-flight work.")]
    private static partial void LogStopping(ILogger logger);

    [LoggerMessage(
        EventId = 6104,
        Level = LogLevel.Information,
        Message = "Operational job scheduler stopped.")]
    private static partial void LogStopped(ILogger logger);
}
