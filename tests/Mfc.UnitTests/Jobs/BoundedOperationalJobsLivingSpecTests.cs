using System.Globalization;
using System.Reflection;
using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Jobs;
using Mfc.Controller.Jobs;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mfc.UnitTests.Jobs;

/// <summary>Living Spec matrix for Issue Set M6-03 AC 1–10 (bounded operational background jobs).</summary>
public sealed class BoundedOperationalJobsLivingSpecTests
{
    private static readonly DateTimeOffset FixedUtc =
        DateTimeOffset.Parse("2026-08-20T12:00:00Z", CultureInfo.InvariantCulture);
    private static OperationalJobsOptions DefaultOptions(int queueDepth = 8)
        => new()
        {
            RecoveryEnabled = true,
            MaxQueueDepth = queueDepth,
            MaxCaptureConcurrency = 16,
            MaxWriteConcurrency = 8,
            DriftPollIntervalSeconds = 300,
            LockHeartbeatIntervalSeconds = 30,
            CleanupIntervalSeconds = 600,
            ExpiredExceptionIntervalSeconds = 60,
            RecoveryScanIntervalSeconds = 15,
            SchedulerIdleSeconds = 1,
            DriftBatchSize = 32,
            RecoveryBatchSize = 16,
            ExpiredExceptionBatchSize = 32,
            OwnerInstanceId = "test-owner",
            SystemActor = "system:operational-jobs",
        };

    [Fact]
    public void Ac1QueuesAreBoundedAndRejectWhenFull()
    {
        BoundedWorkBag<OperationalJobWorkItem> queue = OperationalJobQueues.Create(capacity: 2);
        DateTimeOffset now = FixedUtc;
        Assert.True(queue.TryEnqueue(new OperationalJobWorkItem
        {
            Kind = OperationalJobKind.DriftCapture,
            EnqueuedAtUtc = now,
        }));
        Assert.True(queue.TryEnqueue(new OperationalJobWorkItem
        {
            Kind = OperationalJobKind.LockHeartbeat,
            EnqueuedAtUtc = now,
        }));
        Assert.True(queue.IsFull);
        Assert.False(queue.TryEnqueue(new OperationalJobWorkItem
        {
            Kind = OperationalJobKind.OperationRecovery,
            EnqueuedAtUtc = now,
        }));
        Assert.Equal(2, queue.Count);
        Assert.Equal(2, queue.Capacity);
    }

    [Fact]
    public void Ac2CaptureConcurrencyHonorsConfiguredMaxDefault16()
    {
        OperationalJobsOptions options = DefaultOptions();
        Assert.Equal(16, options.MaxCaptureConcurrency);
        Assert.Equal(8, options.MaxWriteConcurrency);

        BoundedWorkBag<OperationalJobWorkItem> queue = OperationalJobQueues.Create(options.MaxQueueDepth);
        OperationalJobSchedulerHostedService scheduler = CreateScheduler(options, queue);
        Assert.Equal(16, scheduler.CaptureConcurrencyLimit);
        Assert.Equal(8, scheduler.WriteConcurrencyLimit);
    }

    [Fact]
    public void Ac3DriftPollingUsesOneGlobalBoundedConfiguration()
    {
        OperationalJobsOptions options = DefaultOptions();
        OperationalJobTickPlanner planner = new();
        DateTimeOffset now = FixedUtc;

        IReadOnlyList<OperationalJobWorkItem> due = planner.Plan(
            options,
            now,
            lastRecoveryUtc: now,
            lastHeartbeatUtc: now,
            lastExpiredUtc: now,
            lastCleanupUtc: now,
            lastDriftUtc: null);

        Assert.Contains(due, static i => i.Kind == OperationalJobKind.DriftCapture);
        Assert.Single(due, static i => i.Kind == OperationalJobKind.DriftCapture);
        Assert.Equal(300, options.DriftPollIntervalSeconds);
        Assert.Equal(32, options.DriftBatchSize);
    }

    [Fact]
    public void Ac4NoPerDeviceComplexSchedules()
    {
        Type optionsType = typeof(OperationalJobsOptions);
        Assert.DoesNotContain(
            optionsType.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static p => p.Name.Contains("PerDevice", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Cron", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("ScheduleMap", StringComparison.OrdinalIgnoreCase));

        Type[] controllerJobTypes = typeof(OperationalJobSchedulerHostedService).Assembly.GetTypes()
            .Where(static t => t.Namespace is not null
                               && t.Namespace.StartsWith("Mfc.Controller.Jobs", StringComparison.Ordinal))
            .ToArray();
        Assert.DoesNotContain(
            controllerJobTypes,
            static t => t.Name.Contains("Cron", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("PerDeviceSchedule", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac5ExpiredExceptionPathHasZeroRouterOsWritePorts()
    {
        Type useCase = typeof(ReconcileExpiredExceptionBindingsJobUseCase);
        Type expire = typeof(Mfc.Application.Policies.ExpireExceptionBindingUseCase);

        foreach (Type type in new[] { useCase, expire })
        {
            ConstructorInfo ctor = type.GetConstructors().Single();
            Assert.DoesNotContain(
                ctor.GetParameters(),
                static p => p.ParameterType.FullName is not null
                            && (p.ParameterType.FullName.Contains("RouterOs", StringComparison.Ordinal)
                                || p.ParameterType.FullName.Contains("IRouterOs", StringComparison.Ordinal)
                                || p.ParameterType.Name.Contains("DeploymentSession", StringComparison.Ordinal)
                                || p.ParameterType.Name.Contains("WatchdogResidue", StringComparison.Ordinal)));
        }

        Assert.DoesNotContain(
            useCase.Assembly.GetReferencedAssemblies(),
            static a => string.Equals(a.Name, "Mfc.RouterOs", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac6CleanupCannotDeleteFirewallArtifacts()
    {
        Assert.True(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget("mfc4.filter.root"));
        Assert.True(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget("mfc6.forward.allow"));
        Assert.True(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget("fwc.input.guard"));
        Assert.Throws<Mfc.Domain.DomainInvariantException>(
            static () => WatchdogResidueCleanupPolicy.EnsureAllowed("mfc4.filter.root"));
    }

    [Fact]
    public void Ac7CleanupCannotDeleteSnapshotsOrAudit()
    {
        Assert.True(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget("snapshot-archive-1"));
        Assert.True(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget("audit-event-log"));
        Assert.True(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget("approved-revision-bundle"));

        string allowed = "mfc-rb-d-0123456789abcdef";
        Assert.True(WatchdogResidueCleanupPolicy.IsAllowedTemporaryWatchdogResource(allowed));
        Assert.False(WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget(allowed));
        WatchdogResidueCleanupPolicy.EnsureAllowed(allowed);
    }

    [Fact]
    public void Ac8RecoveryPriorityHigherThanDriftPolling()
    {
        Assert.True(OperationalJobKind.OperationRecovery < OperationalJobKind.DriftCapture);

        BoundedWorkBag<OperationalJobWorkItem> queue = OperationalJobQueues.Create(8);
        DateTimeOffset now = FixedUtc;
        Assert.True(queue.TryEnqueue(new OperationalJobWorkItem
        {
            Kind = OperationalJobKind.DriftCapture,
            EnqueuedAtUtc = now,
        }));
        Assert.True(queue.TryEnqueue(new OperationalJobWorkItem
        {
            Kind = OperationalJobKind.OperationRecovery,
            EnqueuedAtUtc = now.AddSeconds(1),
        }));

        Assert.True(queue.TryDequeue(out OperationalJobWorkItem? first));
        Assert.Equal(OperationalJobKind.OperationRecovery, first!.Kind);
        Assert.True(queue.TryDequeue(out OperationalJobWorkItem? second));
        Assert.Equal(OperationalJobKind.DriftCapture, second!.Kind);

        OperationalJobTickPlanner planner = new();
        IReadOnlyList<OperationalJobWorkItem> planned = planner.Plan(
            DefaultOptions(),
            now,
            lastRecoveryUtc: null,
            lastHeartbeatUtc: now,
            lastExpiredUtc: now,
            lastCleanupUtc: now,
            lastDriftUtc: null);
        Assert.Equal(OperationalJobKind.OperationRecovery, planned[0].Kind);
        Assert.Contains(planned, static i => i.Kind == OperationalJobKind.DriftCapture);
        Assert.True(planned.ToList().FindIndex(static i => i.Kind == OperationalJobKind.OperationRecovery)
                    < planned.ToList().FindIndex(static i => i.Kind == OperationalJobKind.DriftCapture));
    }

    [Fact]
    public async Task Ac9ShutdownCancelsJobsCleanly()
    {
        OperationalJobsOptions options = DefaultOptions();
        options.SchedulerIdleSeconds = 60;
        BoundedWorkBag<OperationalJobWorkItem> queue = OperationalJobQueues.Create(options.MaxQueueDepth);
        OperationalJobSchedulerHostedService scheduler = CreateScheduler(options, queue);

        TaskCompletionSource delayEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.DelayAsync = async (_, ct) =>
        {
            delayEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        };

        using CancellationTokenSource cts = new();
        Task run = scheduler.StartAsync(cts.Token);
        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();
        await scheduler.StopAsync(CancellationToken.None);
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void Ac10NoMessageBrokerOrJobFramework()
    {
        Assembly controller = typeof(OperationalJobSchedulerHostedService).Assembly;
        string[] forbidden =
        [
            "Hangfire.Core",
            "Hangfire.AspNetCore",
            "Quartz",
            "Quartz.Extensions.Hosting",
            "MassTransit",
            "RabbitMQ.Client",
            "Confluent.Kafka",
            "NServiceBus",
        ];
        foreach (string name in forbidden)
        {
            Assert.DoesNotContain(
                controller.GetReferencedAssemblies(),
                a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        Assert.True(typeof(IHostedService).IsAssignableFrom(typeof(OperationalJobSchedulerHostedService)));
        Assert.True(typeof(BackgroundService).IsAssignableFrom(typeof(OperationalJobSchedulerHostedService)));

        Type cleanupPort = typeof(IWatchdogResidueCleanupPort);
        Assert.Equal(
            nameof(IWatchdogResidueCleanupPort.RemoveDisabledTemporaryWatchdogResourcesAsync),
            cleanupPort.GetMethods().Single(m => m.DeclaringType == cleanupPort).Name);
    }

    [Fact]
    public async Task CleanupUseCaseRejectsForbiddenTargetsWithoutCallingPort()
    {
        RecordingCleanupPort port = new();
        CleanupDisabledWatchdogResidueJobUseCase useCase = new(port, new MissingDeviceStore());
        var result = await useCase.ExecuteAsync(
            Guid.NewGuid(),
            ["mfc4.filter.root", "snapshot-x", "audit-y"]);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.RemovedNames);
        Assert.Equal(3, result.Value.RejectedNames.Count);
        Assert.Equal(0, port.Calls);
    }

    private static OperationalJobSchedulerHostedService CreateScheduler(
        OperationalJobsOptions options,
        BoundedWorkBag<OperationalJobWorkItem> queue)
    {
        ServiceCollection services = new();
        services.AddSingleton(Options.Create(options));
        services.AddSingleton<IOptionsMonitor<OperationalJobsOptions>>(
            new StaticOptionsMonitor(options));
        ServiceProvider sp = services.BuildServiceProvider();
        return new OperationalJobSchedulerHostedService(
            queue,
            new OperationalJobTickPlanner(),
            new OperationalJobExecutor(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IOptionsMonitor<OperationalJobsOptions>>()),
            sp.GetRequiredService<IOptionsMonitor<OperationalJobsOptions>>(),
            NullLogger<OperationalJobSchedulerHostedService>.Instance);
    }

    private sealed class StaticOptionsMonitor(OperationalJobsOptions current) : IOptionsMonitor<OperationalJobsOptions>
    {
        public OperationalJobsOptions CurrentValue { get; } = current;

        public OperationalJobsOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<OperationalJobsOptions, string?> listener) => null;
    }

    private sealed class RecordingCleanupPort : IWatchdogResidueCleanupPort
    {
        public int Calls { get; private set; }

        public Task<WatchdogResidueCleanupResult> RemoveDisabledTemporaryWatchdogResourcesAsync(
            DeviceId deviceId,
            IReadOnlyList<string> candidateNames,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new WatchdogResidueCleanupResult
            {
                Succeeded = true,
                RemovedNames = candidateNames.ToArray(),
            });
        }
    }

    private sealed class MissingDeviceStore : Mfc.Application.Abstractions.Persistence.IDeviceStore
    {
        public Task AddAsync(Mfc.Domain.Inventory.Device device, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Mfc.Domain.Inventory.Device device, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Mfc.Domain.Inventory.Device?> GetAsync(DeviceId id, CancellationToken cancellationToken = default)
            => Task.FromResult<Mfc.Domain.Inventory.Device?>(null);

        public Task<IReadOnlyList<Mfc.Domain.Inventory.Device>> ListByNodeAsync(
            NodeId nodeId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Mfc.Domain.Inventory.Device>>([]);
    }
}
