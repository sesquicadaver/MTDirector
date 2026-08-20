using System.ComponentModel.DataAnnotations;

namespace Mfc.Controller.Jobs;

/// <summary>
/// Global bounded operational background jobs (E2E §49–§50 / M6-03).
/// One configuration for all devices — no per-device cron schedules.
/// </summary>
public sealed class OperationalJobsOptions
{
    public const string SectionName = "OperationalJobs";

    /// <summary>When false, recovery scan is skipped (other jobs still run).</summary>
    public bool RecoveryEnabled { get; set; } = true;

    /// <summary>Fixed capacity for the in-process work queue (fail-closed when full).</summary>
    [Range(1, 100_000)]
    public int MaxQueueDepth { get; set; } = 256;

    /// <summary>Max concurrent device captures / drift detections (Spec §50 default 16).</summary>
    [Range(1, 1024)]
    public int MaxCaptureConcurrency { get; set; } = 16;

    /// <summary>Max concurrent Node write operations (Spec §50 default 8).</summary>
    [Range(1, 1024)]
    public int MaxWriteConcurrency { get; set; } = 8;

    /// <summary>Global drift poll interval in seconds.</summary>
    [Range(1, 86_400)]
    public int DriftPollIntervalSeconds { get; set; } = 300;

    /// <summary>Durable lock heartbeat interval in seconds.</summary>
    [Range(1, 3600)]
    public int LockHeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>Disabled watchdog residue cleanup interval in seconds.</summary>
    [Range(1, 86_400)]
    public int CleanupIntervalSeconds { get; set; } = 600;

    /// <summary>Expired-exception reconciliation interval in seconds.</summary>
    [Range(1, 86_400)]
    public int ExpiredExceptionIntervalSeconds { get; set; } = 60;

    /// <summary>Nonterminal operation recovery scan interval in seconds.</summary>
    [Range(1, 3600)]
    public int RecoveryScanIntervalSeconds { get; set; } = 15;

    /// <summary>Scheduler tick / idle delay when no due work (seconds).</summary>
    [Range(1, 60)]
    public int SchedulerIdleSeconds { get; set; } = 1;

    [Range(1, 10_000)]
    public int DriftBatchSize { get; set; } = 32;

    [Range(1, 10_000)]
    public int RecoveryBatchSize { get; set; } = 16;

    [Range(1, 10_000)]
    public int ExpiredExceptionBatchSize { get; set; } = 32;

    /// <summary>Owner id stamped on deployment locks for heartbeat refresh.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(128)]
    public string OwnerInstanceId { get; set; } = "mfc-controller";

    /// <summary>System actor used for authorized job use cases.</summary>
    [Required]
    [MinLength(1)]
    public string SystemActor { get; set; } = "system:operational-jobs";
}

