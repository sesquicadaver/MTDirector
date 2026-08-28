using System.Text;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Jobs;

/// <summary>
/// Live API-SSL transport for temporary watchdog residue cleanup (P2-09).
/// Paths are fixed via <see cref="WatchdogResidueCleanupPaths"/>; no free-form commands.
/// </summary>
public sealed class RouterOsWatchdogResidueCleanupChannel : IWatchdogResidueCleanupChannel
{
    private readonly RosSession _session;

    public RouterOsWatchdogResidueCleanupChannel(RosSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public async Task<IReadOnlyDictionary<string, string>> SendAsync(
        WatchdogResidueWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        string command = WatchdogResidueCleanupPaths.Fixed(path);
        (string Name, string Value)[] sent = attributes
            .Select(static a => (a.Key, a.Value))
            .ToArray();

        RosCommandResult result = await _session.ExecuteAsync(
            command,
            sent.Length == 0 ? null : sent,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        EnsureSuccess(result, command);
        if (result.Records.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["ok"] = "true" };
        }

        return SentenceToDictionary(result.Records[0]);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
        WatchdogResidueReadSurface surface,
        CancellationToken cancellationToken = default)
    {
        string command = WatchdogResidueCleanupPaths.Fixed(surface);
        RosCommandResult result = await _session.ExecuteAsync(command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(result, command);
        return result.Records.Select(static s => (IReadOnlyDictionary<string, string>)SentenceToDictionary(s)).ToArray();
    }

    private static void EnsureSuccess(RosCommandResult result, string command)
    {
        if (result.Traps.Count > 0)
        {
            string message = string.Join(
                "; ",
                result.Traps.Select(static trap =>
                    string.Join(
                        ", ",
                        trap.Attributes.Select(static a =>
                            $"{Encoding.ASCII.GetString(a.Name.Span)}={Encoding.UTF8.GetString(a.Value.Span)}"))));
            throw new InvalidOperationException($"RouterOS trap on '{command}': {message}.");
        }

        if (result.Lifecycle != RosCommandLifecycle.Completed)
        {
            throw new InvalidOperationException(
                $"RouterOS command '{command}' ended with {result.Lifecycle}: {result.Error?.Message}.");
        }
    }

    internal static Dictionary<string, string> SentenceToDictionary(RosSentence sentence)
    {
        Dictionary<string, string> row = new(StringComparer.Ordinal);
        foreach (RosAttributeEntry attribute in sentence.Attributes)
        {
            string name = Encoding.ASCII.GetString(attribute.Name.Span);
            row[name] = Encoding.UTF8.GetString(attribute.Value.Span);
        }

        return row;
    }
}
