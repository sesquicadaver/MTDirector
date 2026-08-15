using System.Text.Json;

namespace Mfc.Domain.Policy;

/// <summary>
/// In-memory MFC-CJ1 policy revision document (Policy Model §33).
/// Zone/address/service/test bodies remain opaque JSON until later M2 issues model them;
/// rules are typed <see cref="PolicyRule"/> (M2-06).
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

    public IReadOnlyList<PolicyRule> Rules { get; }

    public IReadOnlyList<JsonElement> Tests { get; }

    /// <summary>Typed exception metadata; null when the object is empty (non-exception or empty draft).</summary>
    public ExceptionMetadata? ExceptionMetadata { get; }

    public PolicyDocument(
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        uint schemaVersion = CurrentSchemaVersion,
        ChainContractSet? chainContracts = null,
        IReadOnlyList<JsonElement>? zoneDefinitions = null,
        IReadOnlyList<JsonElement>? addressObjects = null,
        IReadOnlyList<JsonElement>? serviceObjects = null,
        IReadOnlyList<PolicyRule>? rules = null,
        IReadOnlyList<JsonElement>? tests = null,
        ExceptionMetadata? exceptionMetadata = null)
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

        IReadOnlyList<PolicyRule> typedRules = rules ?? [];
        if (typedRules.Count > 0)
        {
            PolicyRuleSet.EnsureContiguousOrdinals(typedRules);
        }

        SchemaVersion = schemaVersion;
        Kind = kind;
        OwnerScope = ownerScope;
        ChainContracts = contracts;
        ZoneDefinitions = zoneDefinitions ?? [];
        AddressObjects = addressObjects ?? [];
        ServiceObjects = serviceObjects ?? [];
        Rules = typedRules;
        Tests = tests ?? [];
        ExceptionMetadata = exceptionMetadata;
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

    /// <summary>Returns a copy with replacement typed rules (draft editing helper).</summary>
    public PolicyDocument WithRules(IReadOnlyList<PolicyRule> rules)
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

    /// <summary>Returns a copy with replacement exception metadata (EXCEPTION drafts).</summary>
    public PolicyDocument WithExceptionMetadata(ExceptionMetadata? exceptionMetadata)
        => new(
            Kind,
            OwnerScope,
            SchemaVersion,
            ChainContracts,
            ZoneDefinitions,
            AddressObjects,
            ServiceObjects,
            Rules,
            Tests,
            exceptionMetadata);
}
