using System.Globalization;

namespace Mfc.Domain.Incident;

/// <summary>
/// On-demand connection-tracking resolver for incident session context (M7.3-03 / next-2 §2).
/// Matches a scripted snapshot only; does not persist or copy the full connection table.
/// </summary>
public static class IncidentSessionContextResolver
{
    public const string AnalyzerVersion = "mfc.incident-session-context.v1";

    /// <summary>Resolves session context for the original flow in <paramref name="query"/>.</summary>
    public static IncidentSessionContextResult Resolve(
        IncidentSessionContextQuery query,
        ConnectionTrackingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query.OriginalFlow);

        if (string.IsNullOrWhiteSpace(query.OriginalFlow.SourceAddress)
            || string.IsNullOrWhiteSpace(query.OriginalFlow.DestinationAddress)
            || string.IsNullOrWhiteSpace(query.OriginalFlow.Protocol))
        {
            throw new DomainInvariantException(
                $"{IncidentSessionContextCodes.MissingOriginalFlow}: original flow requires protocol, source, and destination.");
        }

        string protocol = NormalizeProtocol(query.OriginalFlow.Protocol);
        string sourceAddress = query.OriginalFlow.SourceAddress.Trim();
        string destinationAddress = query.OriginalFlow.DestinationAddress.Trim();
        ushort? sourcePort = query.OriginalFlow.SourcePort;
        ushort? destinationPort = query.OriginalFlow.DestinationPort;

        List<ConnectionTrackingEntryFact> matches = snapshot.Entries
            .Where(e => MatchesOriginalFlow(e, protocol, sourceAddress, sourcePort, destinationAddress, destinationPort))
            .ToList();

        if (matches.Count == 0)
        {
            return new IncidentSessionContextResult
            {
                Session = null,
                VisibilityStatus = SessionVisibilityStatus.NotObserved,
                Findings =
                [
                    new IncidentSessionContextFinding
                    {
                        Code = IncidentSessionContextCodes.SessionNotFound,
                        Message = "No connection-tracking entry matches the original flow.",
                        Subject = FormatFlow(protocol, sourceAddress, sourcePort, destinationAddress, destinationPort),
                    },
                ],
            };
        }

        if (matches.Count > 1)
        {
            return new IncidentSessionContextResult
            {
                Session = null,
                VisibilityStatus = SessionVisibilityStatus.Partial,
                Findings =
                [
                    new IncidentSessionContextFinding
                    {
                        Code = IncidentSessionContextCodes.SessionAmbiguous,
                        Message = $"Multiple connection-tracking entries ({matches.Count}) match the original flow.",
                        Subject = FormatFlow(protocol, sourceAddress, sourcePort, destinationAddress, destinationPort),
                    },
                ],
            };
        }

        ConnectionTrackingEntryFact entry = matches[0];
        List<IncidentSessionContextFinding> findings = [];
        SessionVisibilityStatus visibility = SessionVisibilityStatus.Full;

        if (entry.HwOffload)
        {
            visibility = SessionVisibilityStatus.Partial;
            findings.Add(new IncidentSessionContextFinding
            {
                Code = IncidentSessionContextCodes.HwOffloadLimitedVisibility,
                Message = "Hardware-offloaded connection may bypass CPU-visible enforcement.",
                Subject = FormatFlow(protocol, sourceAddress, sourcePort, destinationAddress, destinationPort),
            });
        }

        if (entry.FastTrack)
        {
            visibility = SessionVisibilityStatus.Partial;
            findings.Add(new IncidentSessionContextFinding
            {
                Code = IncidentSessionContextCodes.FastTrackLimitedVisibility,
                Message = "FastTrack connection may not be fully enforceable by filter policy.",
                Subject = FormatFlow(protocol, sourceAddress, sourcePort, destinationAddress, destinationPort),
            });
        }

        findings.Add(new IncidentSessionContextFinding
        {
            Code = IncidentSessionContextCodes.SessionResolved,
            Message = "Connection-tracking session context resolved.",
            Subject = FormatFlow(protocol, sourceAddress, sourcePort, destinationAddress, destinationPort),
        });

        FlowTuple? replyFlow = BuildReplyFlow(entry);
        IncidentSessionContext session = new()
        {
            Protocol = entry.Protocol,
            OriginalFlow = query.OriginalFlow,
            ReplyFlow = replyFlow,
            ConnectionState = entry.ConnectionState,
            Timeout = entry.Timeout,
            SrcNatActive = entry.SrcNatActive,
            DstNatActive = entry.DstNatActive,
            FastTrack = entry.FastTrack,
            HwOffload = entry.HwOffload,
            ConnectionMark = entry.ConnectionMark,
            RoutingMark = entry.RoutingMark,
            VisibilityStatus = visibility,
        };

        return new IncidentSessionContextResult
        {
            Session = session,
            VisibilityStatus = visibility,
            Findings = findings,
        };
    }

    internal static bool MatchesOriginalFlow(
        ConnectionTrackingEntryFact entry,
        string protocol,
        string sourceAddress,
        ushort? sourcePort,
        string destinationAddress,
        ushort? destinationPort)
    {
        if (!string.Equals(NormalizeProtocol(entry.Protocol), protocol, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(entry.OriginalSourceAddress, sourceAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(entry.OriginalDestinationAddress, destinationAddress, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (sourcePort is ushort expectedSourcePort
            && entry.OriginalSourcePort is ushort actualSourcePort
            && expectedSourcePort != actualSourcePort)
        {
            return false;
        }

        if (destinationPort is ushort expectedDestinationPort
            && entry.OriginalDestinationPort is ushort actualDestinationPort
            && expectedDestinationPort != actualDestinationPort)
        {
            return false;
        }

        return true;
    }

    private static FlowTuple? BuildReplyFlow(ConnectionTrackingEntryFact entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ReplySourceAddress)
            && string.IsNullOrWhiteSpace(entry.ReplyDestinationAddress))
        {
            return null;
        }

        return FlowTuple.Create(
            sourceAddress: entry.ReplySourceAddress,
            sourcePort: entry.ReplySourcePort,
            destinationAddress: entry.ReplyDestinationAddress,
            destinationPort: entry.ReplyDestinationPort,
            protocol: entry.Protocol);
    }

    private static string NormalizeProtocol(string protocol) => protocol.Trim().ToLowerInvariant();

    private static string FormatFlow(
        string protocol,
        string sourceAddress,
        ushort? sourcePort,
        string destinationAddress,
        ushort? destinationPort)
        => $"{protocol} {sourceAddress}:{sourcePort?.ToString(CultureInfo.InvariantCulture) ?? "*"} -> {destinationAddress}:{destinationPort?.ToString(CultureInfo.InvariantCulture) ?? "*"}";
}
