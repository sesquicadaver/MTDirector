using Mfc.Domain.Incident;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps on-demand connection-tracking RouterOS reads into <see cref="ConnectionTrackingSnapshot"/> (M7.3-03).
/// </summary>
public static class ConnectionTrackingSnapshotMapper
{
    public static ConnectionTrackingSnapshot Map(IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> reads)
    {
        ArgumentNullException.ThrowIfNull(reads);
        List<ConnectionTrackingEntryFact> entries = [];
        entries.AddRange(MapEntries(Get(reads, RosReadCommandId.Ipv4FirewallConnections)));
        entries.AddRange(MapEntries(Get(reads, RosReadCommandId.Ipv6FirewallConnections)));
        return new ConnectionTrackingSnapshot { Entries = entries };
    }

    private static RosReadCommandResult Get(IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> reads, RosReadCommandId id)
        => reads.TryGetValue(id, out RosReadCommandResult? result) ? result : Empty(id);

    private static RosReadCommandResult Empty(RosReadCommandId id)
        => new()
        {
            CommandId = id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = [],
            SessionInvalidated = false,
            Error = null,
        };

    private static IEnumerable<ConnectionTrackingEntryFact> MapEntries(RosReadCommandResult result)
    {
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = row.KnownProperties
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            string? protocol = Get(known, "protocol");
            string? src = Get(known, "src-address");
            string? dst = Get(known, "dst-address");
            if (string.IsNullOrWhiteSpace(protocol) || string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
            {
                continue;
            }

            (string sourceAddress, ushort? sourcePort) = ParseEndpoint(src);
            (string destinationAddress, ushort? destinationPort) = ParseEndpoint(dst);
            (string? replySourceAddress, ushort? replySourcePort) = ParseOptionalEndpoint(Get(known, "reply-src-address"));
            (string? replyDestinationAddress, ushort? replyDestinationPort) = ParseOptionalEndpoint(Get(known, "reply-dst-address"));

            yield return new ConnectionTrackingEntryFact
            {
                Protocol = protocol.Trim(),
                OriginalSourceAddress = sourceAddress,
                OriginalSourcePort = sourcePort,
                OriginalDestinationAddress = destinationAddress,
                OriginalDestinationPort = destinationPort,
                ReplySourceAddress = replySourceAddress,
                ReplySourcePort = replySourcePort,
                ReplyDestinationAddress = replyDestinationAddress,
                ReplyDestinationPort = replyDestinationPort,
                ConnectionState = Get(known, "tcp-state"),
                Timeout = Get(known, "timeout"),
                SrcNatActive = IsTruthy(Get(known, "srcnat")),
                DstNatActive = IsTruthy(Get(known, "dstnat")),
                FastTrack = IsTruthy(Get(known, "fasttrack")),
                HwOffload = IsTruthy(Get(known, "hw-offload")),
                ConnectionMark = Get(known, "connection-mark"),
                RoutingMark = Get(known, "routing-mark"),
            };
        }
    }

    private static (string Address, ushort? Port) ParseEndpoint(string value)
    {
        int colon = value.LastIndexOf(':');
        if (colon <= 0 || colon == value.Length - 1)
        {
            return (value.Trim(), null);
        }

        string address = value[..colon].Trim();
        if (ushort.TryParse(value[(colon + 1)..], out ushort port))
        {
            return (address, port);
        }

        return (value.Trim(), null);
    }

    private static (string? Address, ushort? Port) ParseOptionalEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        (string address, ushort? port) = ParseEndpoint(value);
        return (address, port);
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "1", StringComparison.Ordinal);

    private static string? Get(Dictionary<string, string> known, string name)
        => known.TryGetValue(name, out string? value) ? value : null;
}
