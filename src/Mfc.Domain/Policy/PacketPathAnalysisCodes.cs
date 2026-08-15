namespace Mfc.Domain.Policy;

/// <summary>Frozen packet-path analysis blockers (next-1 / N1-04) for managed FORWARD.</summary>
public static class PacketPathAnalysisCodes
{
    public const string SeverityBlocker = "BLOCKER";

    /// <summary>HARDWARE_OFFLOADED_PATH — managed FORWARD is not proven on the CPU firewall.</summary>
    public const string BypassesIpFirewall = "PACKET_PATH_BYPASSES_IP_FIREWALL";

    /// <summary>INDETERMINATE — packet path through the IP firewall is not proven.</summary>
    public const string NotProven = "PACKET_PATH_NOT_PROVEN";

    /// <summary>Packet-path codes that must map to FailedPrecondition.</summary>
    public static bool IsFailedPrecondition(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        return code.StartsWith("PACKET_PATH_", StringComparison.Ordinal);
    }
}
