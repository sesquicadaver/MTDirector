using System.Text.Json;

namespace Mfc.Domain.Policy;

/// <summary>
/// In-memory MFC-CJ1 policy revision document (Policy Model §33).
/// Object/rule/test bodies remain opaque JSON until later M2 issues model them.
/// </summary>
public sealed class PolicyDocument
{
    public const uint CurrentSchemaVersion = 1;

    public const string SchemaName = "mfc.policy.v1";

    public uint SchemaVersion { get; }

    public PolicyKind Kind { get; }

    public PolicyOwnerScope OwnerScope { get; }

    public ChainContractSet ChainContracts { get; }

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
        ChainContractSet? chainContracts = null,
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

        ChainContractSet contracts = chainContracts ?? (
            kind == PolicyKind.CompanyBaseline
                ? ChainContractSet.Empty
                : ChainContractSet.ForNonBaseline(kind));
        contracts.EnsureCannotBeChangedBy(kind);

        SchemaVersion = schemaVersion;
        Kind = kind;
        OwnerScope = ownerScope;
        ChainContracts = contracts;
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

    /// <summary>Company baseline document with an explicit chain-contract set.</summary>
    public static PolicyDocument CreateCompanyBaseline(
        ChainContractSet chainContracts,
        uint schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(chainContracts);
        return new PolicyDocument(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            schemaVersion,
            chainContracts);
    }

    /// <summary>Returns a copy with replacement chain contracts (COMPANY_BASELINE only).</summary>
    public PolicyDocument WithChainContracts(ChainContractSet chainContracts)
    {
        ArgumentNullException.ThrowIfNull(chainContracts);
        if (Kind != PolicyKind.CompanyBaseline)
        {
            throw new DomainInvariantException(
                "Only COMPANY_BASELINE may set chain contracts; Site/Node overlays cannot change contract.");
        }

        return new PolicyDocument(
            Kind,
            OwnerScope,
            SchemaVersion,
            chainContracts,
            ZoneDefinitions,
            AddressObjects,
            ServiceObjects,
            Rules,
            Tests,
            ExceptionMetadata);
    }

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
