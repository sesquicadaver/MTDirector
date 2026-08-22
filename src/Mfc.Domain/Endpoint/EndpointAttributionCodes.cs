namespace Mfc.Domain.Endpoint;

/// <summary>Finding codes for endpoint attribution resolver (M7.2-01 / next-2 §3).</summary>
public static class EndpointAttributionCodes
{
    public const string IpUnresolved = "ENDPOINT_ATTRIBUTION_IP_UNRESOLVED";
    public const string MacAmbiguous = "ENDPOINT_ATTRIBUTION_MAC_AMBIGUOUS";
    public const string MacUnresolved = "ENDPOINT_ATTRIBUTION_MAC_UNRESOLVED";
    public const string VlanPartial = "ENDPOINT_ATTRIBUTION_VLAN_PARTIAL";
    public const string BridgePartial = "ENDPOINT_ATTRIBUTION_BRIDGE_PARTIAL";
    public const string VethPartial = "ENDPOINT_ATTRIBUTION_VETH_PARTIAL";
    public const string VpnPartial = "ENDPOINT_ATTRIBUTION_VPN_PARTIAL";
    public const string UnsupportedFamily = "ENDPOINT_ATTRIBUTION_UNSUPPORTED_FAMILY";
}
