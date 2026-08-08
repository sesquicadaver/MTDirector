namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// Typed management endpoint (host + TCP port). Port defaults to RouterOS API-SSL 8729.
/// </summary>
public sealed class ManagementEndpoint : IEquatable<ManagementEndpoint>
{
    public const ushort DefaultApiSslPort = 8729;

    public HostNameOrIp Host { get; }

    public ushort Port { get; }

    private ManagementEndpoint(HostNameOrIp host, ushort port)
    {
        Host = host;
        Port = port;
    }

    public static ManagementEndpoint Create(HostNameOrIp host, ushort port = DefaultApiSslPort)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (port == 0)
        {
            throw new DomainInvariantException("management_port must be between 1 and 65535.");
        }

        return new ManagementEndpoint(host, port);
    }

    public static ManagementEndpoint Create(string host, ushort port = DefaultApiSslPort)
        => Create(HostNameOrIp.Create(host), port);

    public bool Equals(ManagementEndpoint? other)
        => other is not null && Host.Equals(other.Host) && Port == other.Port;

    public override bool Equals(object? obj) => obj is ManagementEndpoint other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Host, Port);

    public override string ToString() => $"{Host}:{Port}";
}
