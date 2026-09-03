using System.Collections.Immutable;
using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Parses MFC-CJ1 filter-artifact canonical bytes into staging drafts (inverse of
/// <see cref="RouterOsFilterArtifactCanonicalWriter"/> / SEC-02).
/// </summary>
public static class RouterOsFilterArtifactReader
{
    public const string UnsupportedShapeCode = "FILTER_ARTIFACT_UNSUPPORTED_SHAPE";

    /// <summary>Parsed body used for staging and observed-hash resealing.</summary>
    public sealed class ParsedBody
    {
        public required string LayoutVersion { get; init; }

        public required string ArtifactId { get; init; }

        public required IReadOnlyList<AddressListArtifactDraft> AddressLists { get; init; }

        public required IReadOnlyList<ChainArtifactDraft> Chains { get; init; }

        public required IReadOnlyList<AnchorTargetArtifact> Anchors { get; init; }
    }

    public static ParsedBody Read(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(utf8Json.ToArray());
            return ReadRoot(doc.RootElement);
        }
        catch (DomainInvariantException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException
                                       or ArgumentException or KeyNotFoundException or OverflowException)
        {
            throw new DomainInvariantException(
                $"{UnsupportedShapeCode}: filter artifact JSON is not a valid MFC-CJ1 payload.",
                ex);
        }
    }

    public static ParsedBody Read(byte[] utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        return Read(utf8Json.AsSpan());
    }

    /// <summary>Converts a frozen address list back into a staging draft.</summary>
    public static AddressListArtifactDraft ToDraft(AddressListArtifact list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new AddressListArtifactDraft
        {
            Family = list.Family,
            Name = list.Name,
            Entries = list.Entries.ToArray(),
        };
    }

    /// <summary>Converts a frozen chain back into a staging draft.</summary>
    public static ChainArtifactDraft ToDraft(ChainArtifact chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        return new ChainArtifactDraft
        {
            Family = chain.Family,
            BuiltInContext = chain.BuiltInContext,
            Name = chain.Name,
            Role = chain.Role,
            Rules = chain.Rules.ToArray(),
        };
    }

    private static ParsedBody ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Unsupported("root must be a JSON object.");
        }

        string schema = RequireString(root, "schema");
        if (!string.Equals(schema, RouterOsFilterArtifactCanonicalWriter.SchemaName, StringComparison.Ordinal))
        {
            throw Unsupported($"unsupported schema '{schema}'.");
        }

        string layoutVersion = RequireString(root, "layoutVersion");
        string artifactId = RequireString(root, "artifactId");
        List<AddressListArtifactDraft> lists = ReadAddressLists(RequireArray(root, "addressLists"));
        List<ChainArtifactDraft> chains = ReadChains(RequireArray(root, "chains"));
        List<AnchorTargetArtifact> anchors = ReadAnchors(RequireArray(root, "anchors"));
        return new ParsedBody
        {
            LayoutVersion = layoutVersion,
            ArtifactId = artifactId,
            AddressLists = lists,
            Chains = chains,
            Anchors = anchors,
        };
    }

    private static List<AddressListArtifactDraft> ReadAddressLists(JsonElement array)
    {
        List<AddressListArtifactDraft> lists = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw Unsupported("addressLists entries must be objects.");
            }

            IpAddressFamily family = RouterOsFilterArtifactIdentity.ParseFamily(RequireString(item, "family"));
            string name = RequireString(item, "name");
            _ = RequireString(item, "contentHash");
            List<AddressListEntryArtifact> entries = [];
            foreach (JsonElement entry in RequireArray(item, "entries").EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    throw Unsupported("address-list entries must be objects.");
                }

                entries.Add(AddressListEntryArtifact.Create(RequireString(entry, "address")));
            }

            lists.Add(new AddressListArtifactDraft
            {
                Family = family,
                Name = name,
                Entries = entries,
            });
        }

        return lists;
    }

    private static List<ChainArtifactDraft> ReadChains(JsonElement array)
    {
        List<ChainArtifactDraft> chains = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw Unsupported("chains entries must be objects.");
            }

            IpAddressFamily family = RouterOsFilterArtifactIdentity.ParseFamily(RequireString(item, "family"));
            FilterBuiltInContext builtIn = RouterOsFilterArtifactIdentity.ParseBuiltIn(
                RequireString(item, "builtInContext"));
            string name = RequireString(item, "name");
            FilterChainArtifactRole role = RouterOsFilterArtifactIdentity.ParseRole(RequireString(item, "role"));
            List<FilterRuleArtifact> rules = [];
            foreach (JsonElement ruleElement in RequireArray(item, "rules").EnumerateArray())
            {
                rules.Add(ReadRule(ruleElement));
            }

            chains.Add(new ChainArtifactDraft
            {
                Family = family,
                BuiltInContext = builtIn,
                Name = name,
                Role = role,
                Rules = rules,
            });
        }

        return chains;
    }

    private static FilterRuleArtifact ReadRule(JsonElement rule)
    {
        if (rule.ValueKind != JsonValueKind.Object)
        {
            throw Unsupported("chain rules must be objects.");
        }

        uint ordinal = RequireUInt32(rule, "ordinal");
        Guid? logicalRuleId = null;
        if (rule.TryGetProperty("logicalRuleId", out JsonElement logicalElement)
            && logicalElement.ValueKind == JsonValueKind.String)
        {
            string? text = logicalElement.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && Guid.TryParse(text, out Guid parsed))
            {
                logicalRuleId = parsed;
            }
        }

        uint? variantIndex = null;
        if (rule.TryGetProperty("variantIndex", out JsonElement variantElement)
            && variantElement.ValueKind == JsonValueKind.Number
            && variantElement.TryGetUInt32(out uint variant))
        {
            variantIndex = variant;
        }

        string? structuralRole = null;
        if (rule.TryGetProperty("structuralRole", out JsonElement structuralElement)
            && structuralElement.ValueKind == JsonValueKind.String)
        {
            structuralRole = structuralElement.GetString();
        }

        ImmutableSortedDictionary<string, string> matchers = ReadSortedMap(RequireObject(rule, "matchers"));
        string action = RequireString(rule, "action");
        ImmutableSortedDictionary<string, string> actionParameters =
            ReadSortedMap(RequireObject(rule, "actionParameters"));
        bool log = RequireBoolean(rule, "log");
        string? logPrefix = null;
        if (rule.TryGetProperty("logPrefix", out JsonElement logPrefixElement)
            && logPrefixElement.ValueKind == JsonValueKind.String)
        {
            logPrefix = logPrefixElement.GetString();
        }

        string comment = RequireString(rule, "comment");
        return FilterRuleArtifact.Create(
            ordinal,
            action,
            comment,
            matchers,
            actionParameters,
            logicalRuleId,
            variantIndex,
            structuralRole,
            log,
            logPrefix);
    }

    private static List<AnchorTargetArtifact> ReadAnchors(JsonElement array)
    {
        List<AnchorTargetArtifact> anchors = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw Unsupported("anchors entries must be objects.");
            }

            anchors.Add(AnchorTargetArtifact.Create(
                RouterOsFilterArtifactIdentity.ParseFamily(RequireString(item, "family")),
                RouterOsFilterArtifactIdentity.ParseBuiltIn(RequireString(item, "builtInChain")),
                RequireString(item, "expectedAnchorComment"),
                RequireString(item, "desiredJumpTarget")));
        }

        return anchors;
    }

    private static ImmutableSortedDictionary<string, string> ReadSortedMap(JsonElement obj)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (JsonProperty property in obj.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw Unsupported($"map value for '{property.Name}' must be a string.");
            }

            map[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return RouterOsFilterArtifactIdentity.NormalizePropertyMap(map, "map");
    }

    private static string RequireString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            throw Unsupported($"missing string '{name}'.");
        }

        string? value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Unsupported($"'{name}' must be non-empty.");
        }

        return value;
    }

    private static JsonElement RequireArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            throw Unsupported($"missing array '{name}'.");
        }

        return element;
    }

    private static JsonElement RequireObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Object)
        {
            throw Unsupported($"missing object '{name}'.");
        }

        return element;
    }

    private static uint RequireUInt32(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.Number)
        {
            throw Unsupported($"missing number '{name}'.");
        }

        if (element.TryGetUInt32(out uint value))
        {
            return value;
        }

        if (element.TryGetInt64(out long asLong)
            && asLong >= 0
            && asLong <= uint.MaxValue)
        {
            return (uint)asLong;
        }

        throw Unsupported($"'{name}' is not a valid uint32.");
    }

    private static bool RequireBoolean(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement element)
            || (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
        {
            throw Unsupported($"missing boolean '{name}'.");
        }

        return element.GetBoolean();
    }

    private static DomainInvariantException Unsupported(string detail)
        => new($"{UnsupportedShapeCode}: {detail}");
}
