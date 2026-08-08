namespace Mfc.RouterOs.Commands;

public enum RosResultShape : byte
{
    Singleton = 0,
    UnorderedCollection = 1,
    OrderedCollection = 2,
    DigestedCollection = 3,
}

public enum RosPassPolicy : byte
{
    Pass1Only = 0,
    BothPasses = 1,
    StabilityGuard = 2,
}

public enum RosRequirement : byte
{
    Required = 0,
    Conditional = 1,
    Optional = 2,
}

/// <summary>Centralized redaction policy for sensitive RouterOS properties.</summary>
public enum RosRedactionPolicy : byte
{
    None = 0,

    /// <summary>Value may be retained in memory for typed mapping but must never be logged.</summary>
    LogRedacted = 1,

    /// <summary>Never requested via .proplist and never stored.</summary>
    Forbidden = 2,
}

public enum RosPropertyClassification : byte
{
    ConfigTyped = 0,
    ConfigOpaque = 1,
    ObservationTyped = 2,
    ObservationDigested = 3,
    CapabilityTyped = 4,
    TransientExcluded = 5,
    RawOnly = 6,
    Forbidden = 7,
}

/// <summary>Single property in a fixed-order property profile.</summary>
public sealed class RosPropertyDefinition
{
    public RosPropertyDefinition(
        string routerOsName,
        RosPropertyClassification classification,
        RosRedactionPolicy redactionPolicy = RosRedactionPolicy.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routerOsName);
        if (classification == RosPropertyClassification.Forbidden
            || redactionPolicy == RosRedactionPolicy.Forbidden)
        {
            throw new ArgumentException(
                "Forbidden properties must not appear in a requestable property profile.",
                nameof(classification));
        }

        RouterOsName = routerOsName;
        Classification = classification;
        RedactionPolicy = redactionPolicy;
    }

    public string RouterOsName { get; }

    public RosPropertyClassification Classification { get; }

    public RosRedactionPolicy RedactionPolicy { get; }
}

/// <summary>Immutable ordered property profile used to build <c>.proplist</c>.</summary>
public sealed class RosPropertyProfile
{
    public RosPropertyProfile(string id, IReadOnlyList<RosPropertyDefinition> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(properties);
        if (properties.Count == 0)
        {
            throw new ArgumentException("Property profile must declare at least one property.", nameof(properties));
        }

        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (RosPropertyDefinition property in properties)
        {
            if (!unique.Add(property.RouterOsName))
            {
                throw new ArgumentException(
                    $"Duplicate property '{property.RouterOsName}' in profile '{id}'.",
                    nameof(properties));
            }
        }

        Id = id;
        Properties = properties;
        ProplistValue = string.Join(',', properties.Select(p => p.RouterOsName));
    }

    public string Id { get; }

    public IReadOnlyList<RosPropertyDefinition> Properties { get; }

    /// <summary>Comma-separated .proplist payload in fixed profile order.</summary>
    public string ProplistValue { get; }

    public bool TryGet(string routerOsName, out RosPropertyDefinition? definition)
    {
        foreach (RosPropertyDefinition property in Properties)
        {
            if (string.Equals(property.RouterOsName, routerOsName, StringComparison.Ordinal))
            {
                definition = property;
                return true;
            }
        }

        definition = null;
        return false;
    }
}

/// <summary>Immutable ordered query-word profile (never built from UI input).</summary>
public sealed class RosQueryProfile
{
    public static RosQueryProfile None { get; } = new("none", Array.Empty<string>());

    public static RosQueryProfile AllRows { get; } = new("all_rows", ["=all="]);

    public static RosQueryProfile StaticRoutes { get; } = new(
        "static_routes",
        ["?static=true", "?dynamic=false", "?#&"]);

    public static RosQueryProfile Ipv4DefaultRoutes { get; } = new(
        "ipv4_default_routes",
        ["?dst-address=0.0.0.0/0"]);

    public static RosQueryProfile Ipv6DefaultRoutes { get; } = new(
        "ipv6_default_routes",
        ["?dst-address=::/0"]);

    public RosQueryProfile(string id, IReadOnlyList<string> wireWords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(wireWords);
        List<(string Name, string Value)> printArguments = [];
        List<string> queries = [];
        foreach (string word in wireWords)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                throw new ArgumentException("Wire word must not be empty.", nameof(wireWords));
            }

            if (word[0] == '?')
            {
                queries.Add(word);
                continue;
            }

            if (word[0] == '=')
            {
                int second = word.IndexOf('=', 1);
                if (second <= 1)
                {
                    throw new ArgumentException($"Invalid print argument '{word}'.", nameof(wireWords));
                }

                printArguments.Add((word[1..second], word[(second + 1)..]));
                continue;
            }

            throw new ArgumentException($"Invalid wire word '{word}'.", nameof(wireWords));
        }

        Id = id;
        WireWords = wireWords;
        PrintArguments = printArguments;
        QueryWords = queries;
    }

    public string Id { get; }

    /// <summary>Original ordered wire words (<c>=…</c> print args and <c>?…</c> queries).</summary>
    public IReadOnlyList<string> WireWords { get; }

    /// <summary>Print arguments derived from <c>=name=value</c> wire words.</summary>
    public IReadOnlyList<(string Name, string Value)> PrintArguments { get; }

    /// <summary>Query words starting with <c>?</c> (significant order).</summary>
    public IReadOnlyList<string> QueryWords { get; }
}

/// <summary>Allowlisted read-command definition. Paths are fixed compile-time constants.</summary>
public sealed class RosReadCommandDefinition
{
    public RosReadCommandDefinition(
        RosReadCommandId id,
        string fixedPath,
        RosResultShape resultShape,
        RosRequirement requirement,
        RosPassPolicy passPolicy,
        RosPropertyProfile propertyProfile,
        RosQueryProfile queryProfile,
        int maxRecords = 10_000,
        int maxPayloadBytes = 2 * 1024 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixedPath);
        if (!fixedPath.StartsWith('/')
            || !fixedPath.EndsWith("/print", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Read commands must be fixed '/…/print' paths.",
                nameof(fixedPath));
        }

        ArgumentNullException.ThrowIfNull(propertyProfile);
        ArgumentNullException.ThrowIfNull(queryProfile);

        Id = id;
        FixedPath = fixedPath;
        ResultShape = resultShape;
        Requirement = requirement;
        PassPolicy = passPolicy;
        PropertyProfile = propertyProfile;
        QueryProfile = queryProfile;
        MaxRecords = maxRecords;
        MaxPayloadBytes = maxPayloadBytes;
    }

    public RosReadCommandId Id { get; }

    public string FixedPath { get; }

    public RosResultShape ResultShape { get; }

    public RosRequirement Requirement { get; }

    public RosPassPolicy PassPolicy { get; }

    public RosPropertyProfile PropertyProfile { get; }

    public RosQueryProfile QueryProfile { get; }

    public int MaxRecords { get; }

    public int MaxPayloadBytes { get; }
}
