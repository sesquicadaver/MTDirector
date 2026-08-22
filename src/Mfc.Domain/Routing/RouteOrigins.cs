namespace Mfc.Domain.Routing;

/// <summary>Route origin classification values (M7.1 Spec §10).</summary>
public static class RouteOrigins
{
    public const string Connected = "CONNECTED";

    public const string Static = "STATIC";

    public const string Dhcp = "DHCP";

    public const string Vpn = "VPN";

    public const string Bgp = "BGP";

    public const string Ospf = "OSPF";

    public const string Rip = "RIP";

    public const string Other = "OTHER";

    public static IReadOnlyList<string> All { get; } =
    [
        Connected,
        Static,
        Dhcp,
        Vpn,
        Bgp,
        Ospf,
        Rip,
        Other,
    ];
}
