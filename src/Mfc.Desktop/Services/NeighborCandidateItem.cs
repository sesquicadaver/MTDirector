namespace Mfc.Desktop.Services;

/// <summary>Desktop presentation of a Controller-suggested MikroTik neighbor (#314).</summary>
public sealed class NeighborCandidateItem
{
    public NeighborCandidateItem(
        string address,
        uint suggestedPort,
        string? identity,
        string? platform,
        string? macAddress,
        string? version,
        string? board,
        string? interfaceName)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
        SuggestedPort = suggestedPort;
        Identity = identity;
        Platform = platform;
        MacAddress = macAddress;
        Version = version;
        Board = board;
        InterfaceName = interfaceName;
    }

    public string Address { get; }

    public uint SuggestedPort { get; }

    public string? Identity { get; }

    public string? Platform { get; }

    public string? MacAddress { get; }

    public string? Version { get; }

    public string? Board { get; }

    public string? InterfaceName { get; }

    public string DisplayText
    {
        get
        {
            string name = string.IsNullOrWhiteSpace(Identity) ? Address : Identity.Trim();
            string platform = string.IsNullOrWhiteSpace(Platform) ? "MikroTik" : Platform.Trim();
            return $"{name} — {Address}:{SuggestedPort} ({platform})";
        }
    }
}
