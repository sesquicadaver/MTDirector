namespace Mfc.RouterOs.Jobs;

/// <summary>
/// Compile-time allowlist of write paths for temporary watchdog residue cleanup (P2-09 / E2E §49).
/// Firewall filter/address-list mutations are intentionally absent.
/// </summary>
public enum WatchdogResidueWritePath : byte
{
    SystemSchedulerSet = 0,
    SystemSchedulerRemove = 1,
    SystemScriptRemove = 2,
}

/// <summary>Print surfaces used for residue lookup (script / scheduler only).</summary>
public enum WatchdogResidueReadSurface : byte
{
    Script = 0,
    Scheduler = 1,
}

/// <summary>Maps <see cref="WatchdogResidueWritePath"/> to fixed RouterOS API sentences.</summary>
public static class WatchdogResidueCleanupPaths
{
    public static string Fixed(WatchdogResidueWritePath path)
        => path switch
        {
            WatchdogResidueWritePath.SystemSchedulerSet => "/system/scheduler/set",
            WatchdogResidueWritePath.SystemSchedulerRemove => "/system/scheduler/remove",
            WatchdogResidueWritePath.SystemScriptRemove => "/system/script/remove",
            _ => throw new InvalidOperationException($"Unsupported watchdog residue write path '{path}'."),
        };

    public static string Fixed(WatchdogResidueReadSurface surface)
        => surface switch
        {
            WatchdogResidueReadSurface.Script => "/system/script/print",
            WatchdogResidueReadSurface.Scheduler => "/system/scheduler/print",
            _ => throw new InvalidOperationException($"Unsupported watchdog residue read surface '{surface}'."),
        };
}

/// <summary>Transport used by residue cleanup; tests substitute a recorder.</summary>
public interface IWatchdogResidueCleanupChannel
{
    Task<IReadOnlyDictionary<string, string>> SendAsync(
        WatchdogResidueWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
        WatchdogResidueReadSurface surface,
        CancellationToken cancellationToken = default);
}
