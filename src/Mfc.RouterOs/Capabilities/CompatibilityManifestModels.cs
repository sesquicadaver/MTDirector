using System.Text.Json.Serialization;

namespace Mfc.RouterOs.Capabilities;

/// <summary>Versioned embedded compatibility manifest (Adapter Spec §38).</summary>
public sealed class CompatibilityManifestDocument
{
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("profileId")]
    public required string ProfileId { get; init; }

    [JsonPropertyName("supportedRouterOsBuilds")]
    public required IReadOnlyList<string> SupportedRouterOsBuilds { get; init; }

    [JsonPropertyName("allowedChannels")]
    public required IReadOnlyList<string> AllowedChannels { get; init; }

    [JsonPropertyName("architectures")]
    public required IReadOnlyList<string> Architectures { get; init; }

    [JsonPropertyName("boardClasses")]
    public required IReadOnlyList<string> BoardClasses { get; init; }

    [JsonPropertyName("requiredMenus")]
    public required IReadOnlyList<string> RequiredMenus { get; init; }

    [JsonPropertyName("requiredProperties")]
    public required IReadOnlyList<string> RequiredProperties { get; init; }

    [JsonPropertyName("commandProfiles")]
    public required IReadOnlyList<string> CommandProfiles { get; init; }

    [JsonPropertyName("propertyProfiles")]
    public required IReadOnlyList<string> PropertyProfiles { get; init; }

    [JsonPropertyName("queryProfiles")]
    public required IReadOnlyList<string> QueryProfiles { get; init; }

    [JsonPropertyName("knownTrapSignatures")]
    public required IReadOnlyList<string> KnownTrapSignatures { get; init; }

    [JsonPropertyName("knownIncompatibilities")]
    public required IReadOnlyList<CompatibilityIncompatibility> KnownIncompatibilities { get; init; }
}

public sealed class CompatibilityIncompatibility
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("matchMajor")]
    public int? MatchMajor { get; init; }

    [JsonPropertyName("matchChannel")]
    public string? MatchChannel { get; init; }

    [JsonPropertyName("effect")]
    public required string Effect { get; init; }
}

/// <summary>Board class used for capability matching (not a write capability grant).</summary>
public enum BoardClass : byte
{
    Router = 0,
    Crs = 1,
    Chr = 2,
    Unknown = 3,
}
