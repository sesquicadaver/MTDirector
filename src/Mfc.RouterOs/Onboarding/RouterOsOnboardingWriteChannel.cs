using System.Text;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Live API-SSL transport for closed onboarding writers (P2-07).
/// Paths are fixed via <see cref="OnboardingWritePaths"/>; no free-form commands.
/// </summary>
public sealed class RouterOsOnboardingWriteChannel : IOnboardingWriteChannel
{
    private readonly RosSession _session;

    public RouterOsOnboardingWriteChannel(RosSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public async Task<IReadOnlyDictionary<string, string>> SendAsync(
        OnboardingWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        string command = OnboardingWritePaths.Fixed(path);
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
        IpAddressFamily family,
        CancellationToken cancellationToken = default)
    {
        string command = family == IpAddressFamily.IPv4
            ? "/ip/firewall/filter/print"
            : "/ipv6/firewall/filter/print";
        RosCommandResult result = await _session.ExecuteAsync(command, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(result, command);
        return result.Records.Select(static s => (IReadOnlyDictionary<string, string>)SentenceToDictionary(s)).ToArray();
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintSystemAsync(
        OnboardingSystemSurface surface,
        CancellationToken cancellationToken = default)
    {
        string command = surface switch
        {
            OnboardingSystemSurface.Script => "/system/script/print",
            OnboardingSystemSurface.Scheduler => "/system/scheduler/print",
            _ => throw new InvalidOperationException($"Unsupported onboarding system surface '{surface}'."),
        };
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
