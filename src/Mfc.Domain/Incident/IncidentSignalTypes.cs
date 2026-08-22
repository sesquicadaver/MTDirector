namespace Mfc.Domain.Incident;

/// <summary>Upstream sensor or analytics source for a normalized incident signal (next-2 §IncidentSignal).</summary>
public enum IncidentSignalSourceType
{
    Siem = 1,
    Ndr = 2,
    Edr = 3,
    Ids = 4,
    RouterOsLog = 5,
    FlowAnalyzer = 6,
    Monitoring = 7,
}

/// <summary>Normalized incident severity (next-2 §IncidentSignal).</summary>
public enum IncidentSeverity
{
    Info = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5,
}

/// <summary>Entity kinds referenced by a normalized incident signal.</summary>
public enum EntityReferenceKind
{
    IpAddress = 1,
    MacAddress = 2,
    Hostname = 3,
    User = 4,
    Domain = 5,
    Url = 6,
    Hash = 7,
    Other = 8,
}

/// <summary>Typed entity reference carried on an incident signal.</summary>
public sealed class EntityReference : IEquatable<EntityReference>
{
    public EntityReferenceKind Kind { get; }

    public string Value { get; }

    private EntityReference(EntityReferenceKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    public static EntityReference Create(EntityReferenceKind kind, string value)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.InvalidEntityKind}: entity kind '{kind}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.MissingEntityValue}: entity value is required.");
        }

        string trimmed = value.Trim();
        if (trimmed.Length > 512)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.EntityValueTooLong}: entity value exceeds 512 characters.");
        }

        return new EntityReference(kind, trimmed);
    }

    public bool Equals(EntityReference? other) =>
        other is not null && Kind == other.Kind && Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityReference other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Value);
}

/// <summary>Five-tuple flow context for correlation (next-2 §IncidentSignal).</summary>
public sealed class FlowTuple : IEquatable<FlowTuple>
{
    public string? SourceAddress { get; }

    public ushort? SourcePort { get; }

    public string? DestinationAddress { get; }

    public ushort? DestinationPort { get; }

    public string? Protocol { get; }

    private FlowTuple(
        string? sourceAddress,
        ushort? sourcePort,
        string? destinationAddress,
        ushort? destinationPort,
        string? protocol)
    {
        SourceAddress = sourceAddress;
        SourcePort = sourcePort;
        DestinationAddress = destinationAddress;
        DestinationPort = destinationPort;
        Protocol = protocol;
    }

    public static FlowTuple Create(
        string? sourceAddress = null,
        ushort? sourcePort = null,
        string? destinationAddress = null,
        ushort? destinationPort = null,
        string? protocol = null)
    {
        string? normalizedSource = NormalizeOptional(sourceAddress);
        string? normalizedDestination = NormalizeOptional(destinationAddress);
        string? normalizedProtocol = NormalizeOptional(protocol);

        if (normalizedSource is null
            && normalizedDestination is null
            && normalizedProtocol is null
            && sourcePort is null
            && destinationPort is null)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.EmptyFlowTuple}: flow tuple must include at least one field.");
        }

        ValidatePort(sourcePort, nameof(sourcePort));
        ValidatePort(destinationPort, nameof(destinationPort));

        return new FlowTuple(
            normalizedSource,
            sourcePort,
            normalizedDestination,
            destinationPort,
            normalizedProtocol);
    }

    public bool Equals(FlowTuple? other) =>
        other is not null
        && SourceAddress == other.SourceAddress
        && SourcePort == other.SourcePort
        && DestinationAddress == other.DestinationAddress
        && DestinationPort == other.DestinationPort
        && Protocol == other.Protocol;

    public override bool Equals(object? obj) => obj is FlowTuple other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(SourceAddress, SourcePort, DestinationAddress, DestinationPort, Protocol);

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length > 256
            ? throw new DomainInvariantException(
                $"{IncidentSignalCodes.FlowFieldTooLong}: flow field exceeds 256 characters.")
            : trimmed;
    }

    private static void ValidatePort(ushort? port, string name)
    {
        if (port is 0)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.InvalidFlowPort}: {name} must be between 1 and 65535.");
        }
    }
}

/// <summary>Indicator carried on an incident signal (hash, signature id, etc.).</summary>
public sealed class Indicator : IEquatable<Indicator>
{
    public string Type { get; }

    public string Value { get; }

    private Indicator(string type, string value)
    {
        Type = type;
        Value = value;
    }

    public static Indicator Create(string type, string value)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.MissingIndicatorType}: indicator type is required.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.MissingIndicatorValue}: indicator value is required.");
        }

        string normalizedType = type.Trim();
        string normalizedValue = value.Trim();
        if (normalizedType.Length > 64)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.IndicatorTypeTooLong}: indicator type exceeds 64 characters.");
        }

        if (normalizedValue.Length > 512)
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.IndicatorValueTooLong}: indicator value exceeds 512 characters.");
        }

        return new Indicator(normalizedType, normalizedValue);
    }

    public bool Equals(Indicator? other) =>
        other is not null && Type == other.Type && Value == other.Value;

    public override bool Equals(object? obj) => obj is Indicator other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, Value);
}
