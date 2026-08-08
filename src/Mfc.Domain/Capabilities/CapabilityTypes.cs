using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Capabilities;

/// <summary>SHA-256 of a <see cref="CapabilityProfile"/>.</summary>
public readonly struct CapabilityHash : IEquatable<CapabilityHash>
{
    private readonly Hash256 _value;

    private CapabilityHash(Hash256 value) => _value = value;

    public Hash256 Value => _value;

    public static CapabilityHash FromDigest(Hash256 digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        return new CapabilityHash(digest);
    }

    public static CapabilityHash FromBytes(ReadOnlySpan<byte> bytes)
        => FromDigest(Hash256.Create(bytes));

    public static CapabilityHash ParseHex(string hex)
        => FromDigest(Hash256.ParseHex(hex));

    public bool Equals(CapabilityHash other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is CapabilityHash other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    public static bool operator ==(CapabilityHash left, CapabilityHash right) => left.Equals(right);

    public static bool operator !=(CapabilityHash left, CapabilityHash right) => !left.Equals(right);
}

/// <summary>
/// Parsed RouterOS version. Channel is separate from numeric components.
/// </summary>
public sealed class RouterOsVersion : IEquatable<RouterOsVersion>
{
    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public string? Channel { get; }

    private RouterOsVersion(int major, int minor, int patch, string? channel)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Channel = channel;
    }

    public static RouterOsVersion Create(int major, int minor, int patch, string? channel = null)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new DomainInvariantException("RouterOS version components must be non-negative.");
        }

        string? normalizedChannel = NormalizeChannel(channel);
        return new RouterOsVersion(major, minor, patch, normalizedChannel);
    }

    /// <summary>
    /// Parses <c>7.16.2</c>, <c>7.16.2-stable</c>, or RouterOS resource form <c>7.16.2 (stable)</c>.
    /// Rejects free-form text.
    /// </summary>
    public static RouterOsVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();
        string numeric = trimmed;
        string? channel = null;

        int openParen = trimmed.IndexOf('(', StringComparison.Ordinal);
        int closeParen = trimmed.LastIndexOf(')');
        if (openParen >= 0 && closeParen > openParen)
        {
            numeric = trimmed[..openParen].Trim();
            channel = trimmed[(openParen + 1)..closeParen].Trim();
        }
        else
        {
            int dash = trimmed.IndexOf('-', StringComparison.Ordinal);
            if (dash >= 0)
            {
                numeric = trimmed[..dash];
                channel = trimmed[(dash + 1)..];
            }
        }

        string[] parts = numeric.Split('.', StringSplitOptions.None);
        if (parts.Length is < 2 or > 3)
        {
            throw new DomainInvariantException("RouterOS version must look like MAJOR.MINOR[.PATCH][-channel].");
        }

        if (!int.TryParse(parts[0], out int major)
            || !int.TryParse(parts[1], out int minor))
        {
            throw new DomainInvariantException("RouterOS version numeric components are invalid.");
        }

        int patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
        {
            throw new DomainInvariantException("RouterOS version patch component is invalid.");
        }

        return Create(major, minor, patch, channel);
    }

    public bool Equals(RouterOsVersion? other)
        => other is not null
           && Major == other.Major
           && Minor == other.Minor
           && Patch == other.Patch
           && string.Equals(Channel, other.Channel, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RouterOsVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Channel);

    public override string ToString()
        => Channel is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Channel}";

    private static string? NormalizeChannel(string? channel)
    {
        if (channel is null)
        {
            return null;
        }

        string trimmed = channel.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainInvariantException("RouterOS version channel, when present, must be non-empty.");
        }

        foreach (char c in trimmed)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('-' or '_'))
            {
                throw new DomainInvariantException("RouterOS version channel contains invalid characters.");
            }
        }

        return trimmed.ToLowerInvariant();
    }
}

/// <summary>
/// Discovery capability profile. Value object — no raw API payload, no credentials.
/// </summary>
public sealed class CapabilityProfile : IEquatable<CapabilityProfile>
{
    private readonly string[] _packages;

    public RouterOsVersion Version { get; }

    public NonEmptyName Architecture { get; }

    public NonEmptyName Model { get; }

    public IReadOnlyList<string> Packages => _packages;

    public bool Ipv6Supported { get; }

    public bool VrrpSupported { get; }

    public bool BridgeSupported { get; }

    public bool ApiSslCertificatePresent { get; }

    public SupportState SupportState { get; }

    public Hash256 CompatibilityManifestHash { get; }

    private CapabilityProfile(
        RouterOsVersion version,
        NonEmptyName architecture,
        NonEmptyName model,
        string[] packages,
        bool ipv6Supported,
        bool vrrpSupported,
        bool bridgeSupported,
        bool apiSslCertificatePresent,
        SupportState supportState,
        Hash256 compatibilityManifestHash)
    {
        Version = version;
        Architecture = architecture;
        Model = model;
        _packages = packages;
        Ipv6Supported = ipv6Supported;
        VrrpSupported = vrrpSupported;
        BridgeSupported = bridgeSupported;
        ApiSslCertificatePresent = apiSslCertificatePresent;
        SupportState = supportState;
        CompatibilityManifestHash = compatibilityManifestHash;
    }

    public static CapabilityProfile Create(
        RouterOsVersion version,
        NonEmptyName architecture,
        NonEmptyName model,
        IEnumerable<string> packages,
        bool ipv6Supported,
        bool vrrpSupported,
        bool bridgeSupported,
        bool apiSslCertificatePresent,
        SupportState supportState,
        Hash256 compatibilityManifestHash)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(compatibilityManifestHash);

        string[] normalized = packages
            .Select(p =>
            {
                if (string.IsNullOrWhiteSpace(p))
                {
                    throw new DomainInvariantException("Capability package names must be non-empty.");
                }

                return p.Trim();
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        return new CapabilityProfile(
            version,
            architecture,
            model,
            normalized,
            ipv6Supported,
            vrrpSupported,
            bridgeSupported,
            apiSslCertificatePresent,
            supportState,
            compatibilityManifestHash);
    }

    public bool Equals(CapabilityProfile? other)
    {
        if (other is null)
        {
            return false;
        }

        return Version.Equals(other.Version)
               && Architecture.Equals(other.Architecture)
               && Model.Equals(other.Model)
               && Ipv6Supported == other.Ipv6Supported
               && VrrpSupported == other.VrrpSupported
               && BridgeSupported == other.BridgeSupported
               && ApiSslCertificatePresent == other.ApiSslCertificatePresent
               && SupportState == other.SupportState
               && CompatibilityManifestHash.Equals(other.CompatibilityManifestHash)
               && _packages.SequenceEqual(other._packages, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj) => obj is CapabilityProfile other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hc = default;
        hc.Add(Version);
        hc.Add(Architecture);
        hc.Add(Model);
        hc.Add(Ipv6Supported);
        hc.Add(VrrpSupported);
        hc.Add(BridgeSupported);
        hc.Add(ApiSslCertificatePresent);
        hc.Add(SupportState);
        hc.Add(CompatibilityManifestHash);
        foreach (string package in _packages)
        {
            hc.Add(package, StringComparer.Ordinal);
        }

        return hc.ToHashCode();
    }
}
