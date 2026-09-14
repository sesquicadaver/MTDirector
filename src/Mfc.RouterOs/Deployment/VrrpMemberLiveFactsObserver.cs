using System.Globalization;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Bounded live facts for VRRP member reachability and independent routed traffic (AUDIT-DEP-03 / Safe Deployment §38).
/// </summary>
public static class VrrpMemberLiveFactsObserver
{
    /// <summary>Lightweight API-SSL liveness via <c>/system/identity/print</c>.</summary>
    public static async Task<bool> ProbeReachableAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            RosReadCommandResult identity = await RosReadCommandExecutor
                .ExecuteAsync(session, RosReadCommandId.SystemIdentity, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return identity.IsSuccess;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return false;
        }
    }

    /// <summary>
    /// True when a non-VRRP, enabled, running interface has proven rx/tx counters &gt; 0.
    /// Missing/failed interface reads leave the fact unproven (false) — fail-closed for STANDBY_ONLY.
    /// </summary>
    public static async Task<bool> ObserveIndependentRoutedTrafficAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            RosReadCommandResult interfaces = await RosReadCommandExecutor
                .ExecuteAsync(session, RosReadCommandId.Interfaces, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!interfaces.IsSuccess)
            {
                return false;
            }

            return HasProvenIndependentRoutedTraffic(interfaces.Records);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return false;
        }
    }

    /// <summary>Pure classifier over allowlisted interface rows (unit-testable).</summary>
    public static bool HasProvenIndependentRoutedTraffic(IReadOnlyList<RosReadRecord> interfaceRows)
    {
        ArgumentNullException.ThrowIfNull(interfaceRows);
        foreach (RosReadRecord row in interfaceRows)
        {
            if (!IsEligibleIndependentTrafficInterface(row))
            {
                continue;
            }

            if (CounterSum(row, "rx-byte", "tx-byte") > 0
                || CounterSum(row, "rx-packet", "tx-packet") > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEligibleIndependentTrafficInterface(RosReadRecord row)
    {
        string? type = Get(row, "type");
        if (string.Equals(type, "vrrp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? disabled = Get(row, "disabled");
        if (string.Equals(disabled, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? running = Get(row, "running");
        return string.Equals(running, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(running, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static long CounterSum(RosReadRecord row, string a, string b)
        => ParseCounter(Get(row, a)) + ParseCounter(Get(row, b));

    private static long ParseCounter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        return long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            && value > 0
            ? value
            : 0;
    }

    private static string? Get(RosReadRecord row, string key)
    {
        if (row.KnownProperties.TryGetValue(key, out string? known) && !string.IsNullOrWhiteSpace(known))
        {
            return known;
        }

        return row.RawProperties.TryGetValue(key, out string? raw) ? raw : null;
    }
}
