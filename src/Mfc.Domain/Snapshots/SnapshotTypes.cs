using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Snapshots;

public readonly record struct SnapshotId(Guid Value)
{
    public static SnapshotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

/// <summary>Lifecycle of a device snapshot capture (Vertical Slice §7.2).</summary>
public enum SnapshotStatus : byte
{
    Queued = 0,
    Connecting = 1,
    Authenticating = 2,
    ReadingPass1 = 3,
    CanonicalizingPass1 = 4,
    ReadingPass2 = 5,
    VerifyingStability = 6,
    Persisting = 7,
    Completed = 8,
    Failed = 9,
    Canceled = 10,
}

/// <summary>SHA-256 of canonical configuration material only (excludes runtime observations).</summary>
public readonly struct ConfigurationHash : IEquatable<ConfigurationHash>
{
    private readonly Hash256 _value;

    private ConfigurationHash(Hash256 value) => _value = value;

    public Hash256 Value => _value;

    public static ConfigurationHash FromDigest(Hash256 digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        return new ConfigurationHash(digest);
    }

    public static ConfigurationHash FromBytes(ReadOnlySpan<byte> bytes)
        => FromDigest(Hash256.Create(bytes));

    public static ConfigurationHash ParseHex(string hex)
        => FromDigest(Hash256.ParseHex(hex));

    public bool Equals(ConfigurationHash other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is ConfigurationHash other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    public static bool operator ==(ConfigurationHash left, ConfigurationHash right) => left.Equals(right);

    public static bool operator !=(ConfigurationHash left, ConfigurationHash right) => !left.Equals(right);
}

/// <summary>SHA-256 of runtime observations only (excludes configuration).</summary>
public readonly struct ObservationHash : IEquatable<ObservationHash>
{
    private readonly Hash256 _value;

    private ObservationHash(Hash256 value) => _value = value;

    public Hash256 Value => _value;

    public static ObservationHash FromDigest(Hash256 digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        return new ObservationHash(digest);
    }

    public static ObservationHash FromBytes(ReadOnlySpan<byte> bytes)
        => FromDigest(Hash256.Create(bytes));

    public static ObservationHash ParseHex(string hex)
        => FromDigest(Hash256.ParseHex(hex));

    public bool Equals(ObservationHash other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is ObservationHash other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    public static bool operator ==(ObservationHash left, ObservationHash right) => left.Equals(right);

    public static bool operator !=(ObservationHash left, ObservationHash right) => !left.Equals(right);
}

/// <summary>SHA-256 of the full snapshot envelope (configuration + observations + capabilities + compatibility).</summary>
public readonly struct SnapshotHash : IEquatable<SnapshotHash>
{
    private readonly Hash256 _value;

    private SnapshotHash(Hash256 value) => _value = value;

    public Hash256 Value => _value;

    public static SnapshotHash FromDigest(Hash256 digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        return new SnapshotHash(digest);
    }

    public static SnapshotHash FromBytes(ReadOnlySpan<byte> bytes)
        => FromDigest(Hash256.Create(bytes));

    public static SnapshotHash ParseHex(string hex)
        => FromDigest(Hash256.ParseHex(hex));

    public bool Equals(SnapshotHash other) => _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is SnapshotHash other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    public static bool operator ==(SnapshotHash left, SnapshotHash right) => left.Equals(right);

    public static bool operator !=(SnapshotHash left, SnapshotHash right) => !left.Equals(right);
}
