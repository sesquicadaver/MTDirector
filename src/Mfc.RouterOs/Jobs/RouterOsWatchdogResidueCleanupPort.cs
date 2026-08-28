using Mfc.Application.Abstractions.Jobs;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.RouterOs.Jobs;

/// <summary>
/// Production <see cref="IWatchdogResidueCleanupPort"/> — allowlisted script/scheduler remove (P2-09).
/// DI registration stays fail-closed (<see cref="NotConfiguredWatchdogResidueCleanupPort"/>) until P2-10.
/// </summary>
public sealed class RouterOsWatchdogResidueCleanupPort : IWatchdogResidueCleanupPort
{
    public const string CleanupFailedCode = "watchdog_residue_cleanup_failed";

    private readonly IRouterOsWatchdogResidueSessionFactory _sessions;

    public RouterOsWatchdogResidueCleanupPort(IRouterOsWatchdogResidueSessionFactory sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        _sessions = sessions;
    }

    /// <inheritdoc />
    public async Task<WatchdogResidueCleanupResult> RemoveDisabledTemporaryWatchdogResourcesAsync(
        DeviceId deviceId,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateNames);
        cancellationToken.ThrowIfCancellationRequested();

        List<string> allowed = [];
        foreach (string raw in candidateNames)
        {
            string name = raw.Trim();
            if (!WatchdogResidueCleanupPolicy.IsAllowedTemporaryWatchdogResource(name)
                || WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget(name))
            {
                return Fail($"Residue cleanup refused forbidden target '{name}'.");
            }

            WatchdogResidueCleanupPolicy.EnsureAllowed(name);
            allowed.Add(name);
        }

        if (allowed.Count == 0)
        {
            return new WatchdogResidueCleanupResult
            {
                Succeeded = true,
                RemovedNames = [],
            };
        }

        try
        {
            await using IRouterOsWatchdogResidueSession session = await _sessions
                .OpenAsync(deviceId, cancellationToken)
                .ConfigureAwait(false);
            if (session.DeviceId != deviceId)
            {
                return Fail($"Residue session device mismatch for '{deviceId}'.");
            }

            List<string> removed = [];
            foreach (string name in OrderForRemoval(allowed))
            {
                bool wasRemoved = IsSchedulerName(name)
                    ? await RemoveSchedulerAsync(session.Channel, name, cancellationToken).ConfigureAwait(false)
                    : await RemoveScriptAsync(session.Channel, name, cancellationToken).ConfigureAwait(false);
                if (wasRemoved)
                {
                    removed.Add(name);
                }
            }

            return new WatchdogResidueCleanupResult
            {
                Succeeded = true,
                RemovedNames = removed,
            };
        }
        catch (InvalidOperationException ex)
        {
            return Fail(ex.Message);
        }
    }

    private static async Task<bool> RemoveSchedulerAsync(
        IWatchdogResidueCleanupChannel channel,
        string name,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? row = await FindAsync(
            channel,
            WatchdogResidueReadSurface.Scheduler,
            name,
            cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        if (!row.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException($"Watchdog scheduler '{name}' is missing .id.");
        }

        if (!IsDisabled(row.GetValueOrDefault("disabled")))
        {
            await channel.SendAsync(
                WatchdogResidueWritePath.SystemSchedulerSet,
                [new(".id", itemId), new("disabled", "yes")],
                cancellationToken).ConfigureAwait(false);

            IReadOnlyDictionary<string, string>? afterDisable = await FindAsync(
                channel,
                WatchdogResidueReadSurface.Scheduler,
                name,
                cancellationToken).ConfigureAwait(false);
            if (afterDisable is null || !IsDisabled(afterDisable.GetValueOrDefault("disabled")))
            {
                throw new InvalidOperationException(
                    $"Watchdog scheduler '{name}' was not disabled before residue remove.");
            }

            itemId = afterDisable.GetValueOrDefault(".id") ?? itemId;
        }

        await channel.SendAsync(
            WatchdogResidueWritePath.SystemSchedulerRemove,
            [new(".id", itemId)],
            cancellationToken).ConfigureAwait(false);

        if (await FindAsync(channel, WatchdogResidueReadSurface.Scheduler, name, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"Watchdog scheduler '{name}' remained after residue remove.");
        }

        return true;
    }

    private static async Task<bool> RemoveScriptAsync(
        IWatchdogResidueCleanupChannel channel,
        string name,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? row = await FindAsync(
            channel,
            WatchdogResidueReadSurface.Script,
            name,
            cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        if (!row.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException($"Watchdog script '{name}' is missing .id.");
        }

        await channel.SendAsync(
            WatchdogResidueWritePath.SystemScriptRemove,
            [new(".id", itemId)],
            cancellationToken).ConfigureAwait(false);

        if (await FindAsync(channel, WatchdogResidueReadSurface.Script, name, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"Watchdog script '{name}' remained after residue remove.");
        }

        return true;
    }

    private static async Task<IReadOnlyDictionary<string, string>?> FindAsync(
        IWatchdogResidueCleanupChannel channel,
        WatchdogResidueReadSurface surface,
        string name,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = await channel
            .PrintAsync(surface, cancellationToken)
            .ConfigureAwait(false);
        return rows.FirstOrDefault(r =>
            string.Equals(r.GetValueOrDefault("name"), name, StringComparison.Ordinal));
    }

    /// <summary>Schedulers before scripts so on-event references do not race.</summary>
    private static IEnumerable<string> OrderForRemoval(IReadOnlyList<string> names)
        => names.Where(IsSchedulerName).Concat(names.Where(static n => !IsSchedulerName(n)));

    private static bool IsSchedulerName(string name)
        => name.StartsWith("mfc-rb-d-", StringComparison.Ordinal)
           || name.StartsWith("mfc-rb-b-", StringComparison.Ordinal)
           || name.StartsWith("mfc-ob-d-", StringComparison.Ordinal)
           || name.StartsWith("mfc-ob-b-", StringComparison.Ordinal)
           || name.StartsWith("mfc-cap-d-", StringComparison.Ordinal);

    private static bool IsDisabled(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static WatchdogResidueCleanupResult Fail(string error)
    {
        _ = error;
        return new WatchdogResidueCleanupResult
        {
            Succeeded = false,
            ErrorCode = CleanupFailedCode,
            RemovedNames = [],
        };
    }
}
