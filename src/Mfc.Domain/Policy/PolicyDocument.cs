using System.Text.Json;

namespace Mfc.Domain.Policy;

/// <summary>
/// In-memory MFC-CJ1 policy revision document (Policy Model §33).
/// Object/rule/test bodies are opaque JSON elements until later M2 issues model them.
/// </summary>
public sealed class PolicyDocument
{
    public const uint CurrentSchemaVersion = 1;

    public const string SchemaName = "mfc.policy.v1";

    public uint SchemaVersion { get; }

    public PolicyKind Kind { get; }

    public PolicyOwnerScope OwnerScope { get; }

    public IReadOnlyList<JsonElement> ChainContracts { get; }

    public IReadOnlyList<JsonElement> ZoneDefinitions { get; }

    public IReadOnlyList<JsonElement> AddressObjects { get; }

    public IReadOnlyList<JsonElement> ServiceObjects { get; }

    public IReadOnlyList<JsonElement> Rules { get; }

    public IReadOnlyList<JsonElement> Tests { get; }

    /// <summary>Exception metadata object; empty for non-exception kinds.</summary>
    public IReadOnlyDictionary<string, string> ExceptionMetadata { get; }

    public PolicyDocument(
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        uint schemaVersion = CurrentSchemaVersion,
        IReadOnlyList<JsonElement>? chainContracts = null,
        IReadOnlyList<JsonElement>? zoneDefinitions = null,
        IReadOnlyList<JsonElement>? addressObjects = null,
        IReadOnlyList<JsonElement>? serviceObjects = null,
        IReadOnlyList<JsonElement>? rules = null,
        IReadOnlyList<JsonElement>? tests = null,
        IReadOnlyDictionary<string, string>? exceptionMetadata = null)
    {
        if (schemaVersion == 0)
        {
            throw new DomainInvariantException("Policy document schema_version must be greater than zero.");
        }

        SchemaVersion = schemaVersion;
        Kind = kind;
        OwnerScope = ownerScope;
        ChainContracts = chainContracts ?? [];
        ZoneDefinitions = zoneDefinitions ?? [];
        AddressObjects = addressObjects ?? [];
        ServiceObjects = serviceObjects ?? [];
        Rules = rules ?? [];
        Tests = tests ?? [];
        ExceptionMetadata = exceptionMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Empty document for a new draft of the given kind/scope.</summary>
    public static PolicyDocument CreateEmpty(PolicyKind kind, PolicyOwnerScope ownerScope)
        => new(kind, ownerScope);

    /// <summary>Returns a copy with an additional opaque rule object (draft editing helper).</summary>
    public PolicyDocument WithRules(IReadOnlyList<JsonElement> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new PolicyDocument(
            Kind,
            OwnerScope,
            SchemaVersion,
            ChainContracts,
            ZoneDefinitions,
            AddressObjects,
            ServiceObjects,
            rules,
            Tests,
            ExceptionMetadata);
    }
}
