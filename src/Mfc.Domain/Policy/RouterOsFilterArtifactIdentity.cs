using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Artifact identity and hash contracts (Compiler Spec §7).
/// Timestamps and Controller-side descriptions are never part of these preimages.
/// </summary>
public static class RouterOsFilterArtifactIdentity
{
    public const string ArtifactSeedPrefix = "mfc.filter.compiler.v1";

    public const string PhysicalSemanticsPrefix = "mfc.filter.physical_semantics.v1";

    public const string AddressListContentPrefix = "mfc.filter.address_list.v1";

    public const string ChainContentPrefix = "mfc.filter.chain.v1";

    public const int ArtifactIdHexLength = 16;

    private static readonly HashSet<string> ForbiddenFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".id",
        "id",
        "numbers",
        ".dead",
    };

    private static readonly HashSet<string> ForbiddenApiCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "set",
        "remove",
        "move",
        "print",
        "listen",
        "export",
        "find",
        "getall",
        "enable",
        "disable",
        "comment",
        "unset",
        "reset",
    };

    /// <summary>
    /// <c>physical_semantics_hash = SHA256(canonical physical semantics)</c> excluding descriptions and timestamps.
    /// </summary>
    public static Hash256 HashPhysicalSemantics(PhysicalSemanticsMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentException.ThrowIfNullOrWhiteSpace(material.LayoutVersion);
        ArgumentNullException.ThrowIfNull(material.CompilerProfileHash);
        ArgumentNullException.ThrowIfNull(material.RuleIds);
        ArgumentNullException.ThrowIfNull(material.ResolvedPredicateDigests);
        ArgumentNullException.ThrowIfNull(material.ResolvedZoneDigests);
        ArgumentNullException.ThrowIfNull(material.ActionDigests);
        ArgumentNullException.ThrowIfNull(material.LoggingDigests);
        ArgumentNullException.ThrowIfNull(material.ChainContractDigests);

        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(PhysicalSemanticsPrefix)),
            ("layoutVersion", w => w.WriteString(material.LayoutVersion.Trim())),
            ("compilerProfileHash", w => w.WriteString(material.CompilerProfileHash.ToString())),
            ("ruleIds", w => WriteGuidArray(w, material.RuleIds.OrderBy(static id => id).ToArray())),
            ("resolvedPredicates", w => WriteSortedStringArray(w, material.ResolvedPredicateDigests)),
            ("resolvedZones", w => WriteSortedStringArray(w, material.ResolvedZoneDigests)),
            ("actions", w => WriteSortedStringArray(w, material.ActionDigests)),
            ("logging", w => WriteSortedStringArray(w, material.LoggingDigests)),
            ("chainContracts", w => WriteSortedStringArray(w, material.ChainContractDigests)),
        ]);
        return Hash256.Create(SHA256.HashData(writer.ToUtf8Bytes()));
    }

    /// <summary>
    /// <c>artifact_id</c> = first 16 lowercase hex characters of
    /// <c>SHA256("mfc.filter.compiler.v1" ‖ profile ‖ semantics ‖ device)</c>.
    /// </summary>
    public static string ComputeArtifactId(
        Hash256 compilerProfileHash,
        Hash256 physicalSemanticsHash,
        DeviceId deviceId)
    {
        ArgumentNullException.ThrowIfNull(compilerProfileHash);
        ArgumentNullException.ThrowIfNull(physicalSemanticsHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, ArtifactSeedPrefix);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(compilerProfileHash.Bytes);
        hasher.AppendData(physicalSemanticsHash.Bytes);
        AppendUtf8(hasher, deviceId.Value.ToString("D"));
        byte[] seed = hasher.GetHashAndReset();
        return Convert.ToHexString(seed).ToLowerInvariant()[..ArtifactIdHexLength];
    }

    /// <summary><c>resource_hash = SHA256(MFC-CJ1 canonical resource document)</c>.</summary>
    public static Hash256 HashResourceDocument(ReadOnlySpan<byte> canonicalBytes)
    {
        if (canonicalBytes.IsEmpty)
        {
            throw new DomainInvariantException("Filter artifact canonical bytes must be non-empty.");
        }

        return Hash256.Create(SHA256.HashData(canonicalBytes));
    }

    public static Hash256 HashAddressListContent(
        IpAddressFamily family,
        IReadOnlyList<AddressListEntryArtifact> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(AddressListContentPrefix)),
            ("family", w => w.WriteString(FormatFamily(family))),
            ("entries", w =>
            {
                w.WriteArrayStart();
                AddressListEntryArtifact[] ordered = entries
                    .OrderBy(static e => e.Address, StringComparer.Ordinal)
                    .ToArray();
                for (int i = 0; i < ordered.Length; i++)
                {
                    if (i > 0)
                    {
                        w.WriteComma();
                    }

                    w.WriteObject([("address", x => x.WriteString(ordered[i].Address))]);
                }

                w.WriteArrayEnd();
            }),
        ]);
        return Hash256.Create(SHA256.HashData(writer.ToUtf8Bytes()));
    }

    /// <summary>
    /// Ordered chain content hash for create-or-verify (Safe Deployment Spec §19 step 8).
    /// Rule order is significant; address-list content hash remains unordered.
    /// </summary>
    public static Hash256 HashChainContent(
        IpAddressFamily family,
        FilterBuiltInContext builtInContext,
        FilterChainArtifactRole role,
        string name,
        IReadOnlyList<FilterRuleArtifact> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rules);
        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(ChainContentPrefix)),
            ("family", w => w.WriteString(FormatFamily(family))),
            ("builtInContext", w => w.WriteString(FormatBuiltIn(builtInContext))),
            ("name", w => w.WriteString(name.Trim())),
            ("role", w => w.WriteString(FormatRole(role))),
            ("rules", w =>
            {
                w.WriteArrayStart();
                for (int i = 0; i < rules.Count; i++)
                {
                    if (i > 0)
                    {
                        w.WriteComma();
                    }

                    FilterRuleArtifact rule = rules[i];
                    List<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties =
                    [
                        ("ordinal", x => x.WriteNumber(rule.Ordinal)),
                    ];
                    properties.Add(("matchers", x => WriteSortedMap(x, rule.Matchers)));
                    properties.Add(("action", x => x.WriteString(rule.Action)));
                    properties.Add(("actionParameters", x => WriteSortedMap(x, rule.ActionParameters)));
                    properties.Add(("log", x => x.WriteBoolean(rule.Log)));
                    if (!string.IsNullOrWhiteSpace(rule.LogPrefix))
                    {
                        properties.Add(("logPrefix", x => x.WriteString(rule.LogPrefix)));
                    }

                    properties.Add(("comment", x => x.WriteString(rule.Comment)));
                    w.WriteObject(properties);
                }

                w.WriteArrayEnd();
            }),
        ]);
        return Hash256.Create(SHA256.HashData(writer.ToUtf8Bytes()));
    }

    private static void WriteSortedMap(CanonicalJsonWriter writer, ImmutableSortedDictionary<string, string> map)
    {
        writer.WriteObjectStart();
        bool first = true;
        foreach ((string key, string value) in map)
        {
            if (!first)
            {
                writer.WriteComma();
            }

            first = false;
            writer.WritePropertyName(key);
            writer.WriteString(value);
        }

        writer.WriteObjectEnd();
    }

    public static void EnsureNotForbiddenField(string fieldName, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        if (ForbiddenFieldNames.Contains(fieldName.Trim()))
        {
            throw new DomainInvariantException(
                $"Filter artifact must not contain RouterOS identity field '{fieldName}'.");
        }

        EnsureNoRouterOsIdToken(value);
    }

    /// <summary>Rejects any string that embeds a RouterOS <c>.id</c> token.</summary>
    public static void EnsureNoRouterOsIdToken(string? value)
    {
        if (value is not null && value.Contains(".id", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainInvariantException("Filter artifact must not embed RouterOS .id values.");
        }
    }

    /// <summary>Generated ownership comments must use <c>mfc:</c>/<c>fwc:</c> markers (Compiler Spec §23).</summary>
    public static void EnsureManagedComment(string comment, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);
        EnsureNoRouterOsIdToken(comment);
        string trimmed = comment.Trim();
        if (!trimmed.StartsWith("mfc:", StringComparison.Ordinal)
            && !trimmed.StartsWith("fwc:", StringComparison.Ordinal))
        {
            throw new DomainInvariantException(
                $"{field} must be a managed marker comment (mfc:/fwc:), not a free-form description.");
        }
    }

    public static void EnsureNotApiCommand(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (ForbiddenApiCommands.Contains(token.Trim()))
        {
            throw new DomainInvariantException(
                $"Filter artifact must not contain API command '{token}'.");
        }
    }

    public static void EnsureAsciiLowerResourceName(string name, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureNoRouterOsIdToken(name);
        foreach (char c in name)
        {
            bool ok = (c is >= 'a' and <= 'z')
                      || (c is >= '0' and <= '9')
                      || c is '.' or '-' or '_';
            if (!ok)
            {
                throw new DomainInvariantException(
                    $"{field} must be lowercase ASCII resource name without display text.");
            }
        }
    }

    public static ImmutableSortedDictionary<string, string> NormalizePropertyMap(
        IReadOnlyDictionary<string, string>? source,
        string kind)
    {
        if (source is null || source.Count == 0)
        {
            return ImmutableSortedDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);
        }

        ImmutableSortedDictionary<string, string>.Builder builder =
            ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new DomainInvariantException($"{kind} key must be non-empty.");
            }

            string trimmedKey = key.Trim();
            EnsureNotForbiddenField(trimmedKey, value);
            EnsureNotApiCommand(trimmedKey);
            if (value is null)
            {
                throw new DomainInvariantException($"{kind} '{trimmedKey}' value must not be null.");
            }

            if (!builder.TryAdd(trimmedKey, value))
            {
                throw new DomainInvariantException($"Duplicate {kind} key '{trimmedKey}'.");
            }
        }

        return builder.ToImmutable();
    }

    public static string FormatFamily(IpAddressFamily family)
        => family switch
        {
            IpAddressFamily.IPv4 => "IPv4",
            IpAddressFamily.IPv6 => "IPv6",
            _ => throw new DomainInvariantException($"Unsupported address family '{family}'."),
        };

    public static string FormatBuiltIn(FilterBuiltInContext context)
        => context switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported built-in context '{context}'."),
        };

    public static string FormatRole(FilterChainArtifactRole role)
        => role switch
        {
            FilterChainArtifactRole.Root => "root",
            FilterChainArtifactRole.CompanyDeny => "company_deny",
            FilterChainArtifactRole.SiteDeny => "site_deny",
            FilterChainArtifactRole.NodeDeny => "node_deny",
            _ => throw new DomainInvariantException($"Unsupported chain role '{role}'."),
        };

    private static void WriteGuidArray(CanonicalJsonWriter writer, Guid[] values)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            writer.WriteString(values[i].ToString("D"));
        }

        writer.WriteArrayEnd();
    }

    private static void WriteSortedStringArray(CanonicalJsonWriter writer, IReadOnlyList<string> values)
    {
        writer.WriteArrayStart();
        string[] ordered = values
            .Select(static v => v?.Trim() ?? string.Empty)
            .Where(static v => v.Length > 0)
            .OrderBy(static v => v, StringComparer.Ordinal)
            .ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            writer.WriteString(ordered[i]);
        }

        writer.WriteArrayEnd();
    }

    private static void AppendUtf8(IncrementalHash hasher, string text)
        => hasher.AppendData(Encoding.UTF8.GetBytes(text));
}
