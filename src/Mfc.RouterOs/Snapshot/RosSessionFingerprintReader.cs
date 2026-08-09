using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Snapshot;

/// <summary>
/// Reads critical configuration menu fingerprints from an authenticated session
/// using only allowlisted read commands and configuration-classified properties.
/// </summary>
public static class RosSessionFingerprintReader
{
    /// <summary>Reads all critical-menu fingerprints with bounded parallel command execution.</summary>
    public static async Task<ConfigurationFingerprintSet> ReadAsync(
        RosSession session,
        StableReadExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);

        HashSet<RosReadCommandId> commandIds = [];
        foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
        {
            foreach (RosReadCommandId id in CriticalConfigurationMenus.CommandsFor(menu))
            {
                if (ConfigurationFingerprintBuilder.IsObservationOnlyCommand(id))
                {
                    throw new InvalidOperationException(
                        $"Observation-only command '{id}' must not be used for stable-read fingerprints.");
                }

                commandIds.Add(id);
            }
        }

        List<RosReadCommandId> ordered = commandIds.OrderBy(static id => (int)id).ToList();
        List<Func<CancellationToken, Task<(RosReadCommandId Id, RosReadCommandResult Result)>>> actions = [];
        foreach (RosReadCommandId commandId in ordered)
        {
            RosReadCommandId captured = commandId;
            actions.Add(async ct =>
            {
                RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
                    session,
                    captured,
                    context.CommandTimeout,
                    ct).ConfigureAwait(false);
                return (captured, result);
            });
        }

        IReadOnlyList<(RosReadCommandId Id, RosReadCommandResult Result)> executed =
            await context.Parallelism.RunAllAsync(actions, cancellationToken).ConfigureAwait(false);

        Dictionary<RosReadCommandId, RosReadCommandResult> map = new();
        foreach ((RosReadCommandId id, RosReadCommandResult result) in executed)
        {
            map[id] = result;
        }

        return ConfigurationFingerprintBuilder.BuildSet(map);
    }
}
