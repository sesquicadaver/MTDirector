using System.Net;
using System.Net.Sockets;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>Exact permanent-anchor jump target captured on a deployment plan (Safe Deployment Spec §9).</summary>
public sealed class AnchorTarget : IEquatable<AnchorTarget>
{
    public AnchorTarget(AnchorKey key, string jumpTarget)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(jumpTarget);
        Key = key;
        JumpTarget = jumpTarget.Trim();
    }

    public AnchorKey Key { get; }

    public string JumpTarget { get; }

    public bool Equals(AnchorTarget? other)
        => other is not null
           && Key.Equals(other.Key)
           && string.Equals(JumpTarget, other.JumpTarget, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AnchorTarget other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Key, JumpTarget);
}

/// <summary>
/// Bounded verification probe recorded on the plan (Safe Deployment Spec §33).
/// Destination is a literal IP for ROUTER_PING / API_SSL — never a DNS hostname.
/// </summary>
public sealed class DeploymentProbe
{
    public const int MinTimeoutMs = 100;

    public const int MaxTimeoutMs = 5000;

    public const int FixedPingCount = 3;

    public DeploymentProbe(
        DeploymentProbeKind kind,
        string destination,
        int timeoutMilliseconds,
        string? sourceAddress = null,
        string? routingTable = null,
        string? @interface = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException($"Unknown deployment probe kind '{kind}'.");
        }

        if (kind is not (DeploymentProbeKind.RouterPing or DeploymentProbeKind.ApiSsl))
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ProbeKindUnsupported}: only API_SSL and ROUTER_PING are supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (timeoutMilliseconds is < MinTimeoutMs or > MaxTimeoutMs)
        {
            throw new DomainInvariantException(
                $"Probe timeout must be between {MinTimeoutMs} and {MaxTimeoutMs} ms.");
        }

        string dest = destination.Trim();
        if (!TryParseLiteralIp(dest, out IPAddress? ip) || ip is null)
        {
            throw new DomainInvariantException(
                $"{DeploymentCodes.ProbeHostnameForbidden}: probe destination must be a literal IP address.");
        }

        if (sourceAddress is not null)
        {
            if (!TryParseLiteralIp(sourceAddress.Trim(), out IPAddress? src) || src is null)
            {
                throw new DomainInvariantException(
                    $"{DeploymentCodes.ProbeHostnameForbidden}: source address must be a literal IP.");
            }

            SourceAddress = src.ToString();
        }

        Kind = kind;
        Destination = ip.ToString();
        TimeoutMilliseconds = timeoutMilliseconds;
        RoutingTable = string.IsNullOrWhiteSpace(routingTable) ? null : routingTable.Trim();
        Interface = string.IsNullOrWhiteSpace(@interface) ? null : @interface.Trim();
        Family = ip.AddressFamily == AddressFamily.InterNetworkV6
            ? IpAddressFamily.IPv6
            : IpAddressFamily.IPv4;
    }

    public DeploymentProbeKind Kind { get; }

    public string Destination { get; }

    public int TimeoutMilliseconds { get; }

    public IpAddressFamily Family { get; }

    public string? SourceAddress { get; }

    public string? RoutingTable { get; }

    public string? Interface { get; }

    public static bool TryParseLiteralIp(string value, out IPAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Reject hostnames: IPAddress.TryParse accepts some odd forms; also ban letters except hex IPv6.
        string trimmed = value.Trim();
        if (trimmed.Any(static c => char.IsLetter(c) && c is not (>= 'a' and <= 'f') and not (>= 'A' and <= 'F')))
        {
            return false;
        }

        return IPAddress.TryParse(trimmed, out address);
    }
}
