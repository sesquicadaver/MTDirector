using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Policies;

public sealed class UpsertAddressObjectCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public Guid? ObjectId { get; init; }

    public required string Name { get; init; }

    public required IpAddressFamily Family { get; init; }

    public required IReadOnlyList<AddressObjectEntryView> Entries { get; init; }

    public string? Description { get; init; }
}

public sealed class UpsertServiceObjectCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public Guid? ObjectId { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ServiceTermView> Terms { get; init; }

    public string? Description { get; init; }
}

public sealed class ReplaceChainContractsCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    public required IReadOnlyList<ChainContractView> Contracts { get; init; }
}

public sealed class ReplacePolicyTestsCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid RevisionId { get; init; }

    public required byte[] ExpectedContentHash { get; init; }

    /// <summary>Raw JSON array string (preferred opacity residual).</summary>
    public string? TestsJson { get; init; }

    /// <summary>Structured list of JSON object strings when TestsJson is null.</summary>
    public IReadOnlyList<string>? TestJsonElements { get; init; }
}

/// <summary>Upserts one address object into a draft revision catalog (M2-18).</summary>
public sealed class UpsertAddressObjectUseCase
{
    public const string Operation = "policy.upsert_address_object";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public UpsertAddressObjectUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _policies = policies;
        _idempotency = idempotency;
        _audit = audit;
    }

    public Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        UpsertAddressObjectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.RevisionId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
            command.ObjectId,
            command.Name,
            family = command.Family.ToString(),
            command.Description,
            entries = command.Entries.Select(static e => new
            {
                e.Kind,
                e.Address,
                e.PrefixLength,
                e.Start,
                e.End,
            }),
        });
        return PolicyCatalogMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _idempotency,
            _audit,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            command.RevisionId,
            command.ExpectedContentHash,
            requestHash,
            (policy, revision, document) =>
            {
                if (policy.Kind == PolicyKind.Exception)
                {
                    throw new DomainInvariantException(
                        "EXCEPTION revisions cannot define address_objects in this milestone.");
                }

                PolicyObjectIdentity owner = PolicyCatalogViewMapper.DeriveObjectIdentity(policy, revision);
                List<AddressEntry> entries = command.Entries
                    .Select(e => PolicyCatalogViewMapper.ToAddressEntry(e, command.Family))
                    .ToList();
                AddressObject typed = command.ObjectId is Guid existingId
                    ? AddressObject.Reconstitute(
                        new AddressObjectId(existingId),
                        owner.OwnerScope,
                        owner.OwnerId,
                        owner.ExceptionRevisionId,
                        NonEmptyName.Create(command.Name),
                        command.Family,
                        command.Description,
                        entries.Select(static e => e.ToInterval()).ToArray())
                    : AddressObject.Create(
                        owner.OwnerScope,
                        owner.OwnerId,
                        owner.ExceptionRevisionId,
                        NonEmptyName.Create(command.Name),
                        command.Family,
                        entries,
                        command.Description);
                JsonElement written = PolicyObjectJsonWriter.WriteAddress(typed);
                List<JsonElement> next = [];
                bool replaced = false;
                foreach (JsonElement element in document.AddressObjects)
                {
                    if (element.ValueKind == JsonValueKind.Object
                        && element.TryGetProperty("id", out JsonElement idElement)
                        && idElement.ValueKind == JsonValueKind.String
                        && Guid.TryParse(idElement.GetString(), out Guid id)
                        && id == typed.Id.Value)
                    {
                        next.Add(written);
                        replaced = true;
                    }
                    else
                    {
                        next.Add(element.Clone());
                    }
                }

                if (!replaced)
                {
                    next.Add(written);
                }

                return (document.WithAddressObjects(next), typed.Id.Value);
            },
            cancellationToken);
    }
}

/// <summary>Upserts one service object into a draft revision catalog (M2-18).</summary>
public sealed class UpsertServiceObjectUseCase
{
    public const string Operation = "policy.upsert_service_object";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public UpsertServiceObjectUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _policies = policies;
        _idempotency = idempotency;
        _audit = audit;
    }

    public Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        UpsertServiceObjectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.RevisionId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
            command.ObjectId,
            command.Name,
            command.Description,
            terms = command.Terms.Select(static t => new
            {
                protocol_any = t.Protocol.Any,
                t.Protocol.Number,
                t.Protocol.CanonicalName,
                source = t.SourcePorts,
                destination = t.DestinationPorts,
                icmp = t.IcmpSelectors,
            }),
        });
        return PolicyCatalogMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _idempotency,
            _audit,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            command.RevisionId,
            command.ExpectedContentHash,
            requestHash,
            (policy, revision, document) =>
            {
                if (policy.Kind == PolicyKind.Exception)
                {
                    throw new DomainInvariantException(
                        "EXCEPTION revisions cannot define service_objects in this milestone.");
                }

                PolicyObjectIdentity owner = PolicyCatalogViewMapper.DeriveObjectIdentity(policy, revision);
                List<ServiceTerm> terms = command.Terms.Select(PolicyCatalogViewMapper.ToServiceTerm).ToList();
                ServiceObject typed = command.ObjectId is Guid existingId
                    ? ServiceObject.Reconstitute(
                        new ServiceObjectId(existingId),
                        owner.OwnerScope,
                        owner.OwnerId,
                        owner.ExceptionRevisionId,
                        NonEmptyName.Create(command.Name),
                        command.Description,
                        terms)
                    : ServiceObject.Create(
                        owner.OwnerScope,
                        owner.OwnerId,
                        owner.ExceptionRevisionId,
                        NonEmptyName.Create(command.Name),
                        terms,
                        command.Description);
                JsonElement written = PolicyObjectJsonWriter.WriteService(typed);
                List<JsonElement> next = [];
                bool replaced = false;
                foreach (JsonElement element in document.ServiceObjects)
                {
                    if (element.ValueKind == JsonValueKind.Object
                        && element.TryGetProperty("id", out JsonElement idElement)
                        && idElement.ValueKind == JsonValueKind.String
                        && Guid.TryParse(idElement.GetString(), out Guid id)
                        && id == typed.Id.Value)
                    {
                        next.Add(written);
                        replaced = true;
                    }
                    else
                    {
                        next.Add(element.Clone());
                    }
                }

                if (!replaced)
                {
                    next.Add(written);
                }

                return (document.WithServiceObjects(next), typed.Id.Value);
            },
            cancellationToken);
    }
}

/// <summary>Replaces COMPANY_BASELINE chain contracts on a draft revision (M2-18).</summary>
public sealed class ReplaceChainContractsUseCase
{
    public const string Operation = "policy.replace_chain_contracts";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public ReplaceChainContractsUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _policies = policies;
        _idempotency = idempotency;
        _audit = audit;
    }

    public Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        ReplaceChainContractsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.RevisionId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
            contracts = command.Contracts.Select(static c => new
            {
                family = c.Family.ToString(),
                chain = c.Chain.ToString(),
                c.DefaultDisposition,
                reject_mode = c.RejectMode?.ToString(),
            }),
        });
        return PolicyCatalogMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _idempotency,
            _audit,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            command.RevisionId,
            command.ExpectedContentHash,
            requestHash,
            (policy, _, document) =>
            {
                if (policy.Kind != PolicyKind.CompanyBaseline)
                {
                    throw new DomainInvariantException(
                        "Only COMPANY_BASELINE may set chain contracts; Site/Node overlays cannot change contract.");
                }

                bool needsMigration = command.Contracts.Any(static c =>
                    string.Equals(c.DefaultDisposition, "RETURN_TO_UNMANAGED", StringComparison.Ordinal));
                PolicyRuntimeMode runtime = needsMigration
                    ? PolicyRuntimeMode.MigrationCoexistence
                    : PolicyRuntimeMode.ManagedOnly;
                List<ChainContract> contracts = [];
                foreach (ChainContractView item in command.Contracts)
                {
                    ChainDefaultDisposition disposition = ParseDisposition(item.DefaultDisposition);
                    contracts.Add(ChainContract.Create(
                        item.Family,
                        item.Chain,
                        disposition,
                        item.RejectMode,
                        runtime));
                }

                ChainContractSet set = ChainContractSet.CreateForCompanyBaseline(contracts, runtime);
                return (document.WithChainContracts(set), policy.Id.Value);
            },
            cancellationToken);
    }

    private static ChainDefaultDisposition ParseDisposition(string text)
        => text.ToUpperInvariant() switch
        {
            "DROP" => ChainDefaultDisposition.Drop,
            "REJECT" => ChainDefaultDisposition.Reject,
            "RETURN_TO_UNMANAGED" => ChainDefaultDisposition.ReturnToUnmanaged,
            _ => throw new DomainInvariantException($"Unknown default disposition '{text}'."),
        };
}

/// <summary>Replaces opaque policy tests JSON on a draft revision (M2-18).</summary>
public sealed class ReplacePolicyTestsUseCase
{
    public const string Operation = "policy.replace_policy_tests";

    private readonly IAuthorizationBoundary _auth;
    private readonly IPolicyStore _policies;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public ReplacePolicyTestsUseCase(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _policies = policies;
        _idempotency = idempotency;
        _audit = audit;
    }

    public Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        ReplacePolicyTestsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.RevisionId,
            content_hash = Convert.ToHexString(command.ExpectedContentHash).ToLowerInvariant(),
            command.TestsJson,
            command.TestJsonElements,
        });
        return PolicyCatalogMutationPipeline.ExecuteAsync(
            _auth,
            _policies,
            _idempotency,
            _audit,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            command.RevisionId,
            command.ExpectedContentHash,
            requestHash,
            (policy, revision, document) =>
            {
                IReadOnlyList<JsonElement> tests = ParseTests(command);
                return (document.WithTests(tests), revision.Id.Value);
            },
            cancellationToken);
    }

    private static List<JsonElement> ParseTests(ReplacePolicyTestsCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.TestsJson))
        {
            using JsonDocument doc = JsonDocument.Parse(command.TestsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new DomainInvariantException("tests_json must be a JSON array.");
            }

            List<JsonElement> items = [];
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                items.Add(item.Clone());
            }

            return items;
        }

        if (command.TestJsonElements is null)
        {
            throw new DomainInvariantException("Provide tests_json or a structured test_json_elements list.");
        }

        List<JsonElement> structured = [];
        foreach (string raw in command.TestJsonElements)
        {
            using JsonDocument doc = JsonDocument.Parse(raw);
            structured.Add(doc.RootElement.Clone());
        }

        return structured;
    }
}

/// <summary>Shared draft catalog mutate → CAS → ReplaceDocument → SaveRevision pipeline.</summary>
internal static class PolicyCatalogMutationPipeline
{
    public static async Task<ApplicationResult<PolicyRevisionView>> ExecuteAsync(
        IAuthorizationBoundary auth,
        IPolicyStore policies,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        string actor,
        string operation,
        Guid idempotencyKey,
        Guid revisionId,
        byte[] expectedContentHash,
        byte[] requestHash,
        Func<Policy, PolicyRevision, PolicyDocument, (PolicyDocument Next, Guid ResourceId)> mutate,
        CancellationToken cancellationToken)
    {
        ApplicationError? authError = await Auth.EnsureAsync(
            auth, actor, ApplicationPermissions.PolicyWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(idempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        ApplicationResult<PolicyRevisionView>? replay = await IdempotencySupport.TryReplayAsync(
            idempotency,
            actor,
            operation,
            idempotencyKey,
            requestHash,
            async (id, ct) => await LoadViewAsync(policies, revisionId, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(policies, revisionId, cancellationToken)
            .ConfigureAwait(false);
        if (loadError is not null)
        {
            return ApplicationResults.Fail(loadError);
        }

        ApplicationError? editable = PolicyRevisionSupport.EnsureEditable(revision!);
        if (editable is not null)
        {
            return ApplicationResults.Fail(editable);
        }

        ApplicationError? cas = PolicyRevisionSupport.EnsureContentHash(revision!, expectedContentHash);
        if (cas is not null)
        {
            return ApplicationResults.Fail(cas);
        }

        Policy? policy = await policies.GetPolicyAsync(revision!.PolicyId, cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Policy '{revision.PolicyId}' was not found."));
        }

        ApplicationResult<PolicyDocument> documentResult = PolicyRevisionSupport.ReadDocument(revision);
        if (documentResult.IsFailure)
        {
            return ApplicationResults.Fail(documentResult.Error!);
        }

        try
        {
            (PolicyDocument next, Guid resourceId) = mutate(policy, revision, documentResult.Value!);
            foreach (PolicyRule rule in next.Rules)
            {
                ApplicationError? catalogError = PolicyRevisionSupport.EnsureAddressServiceCatalog(
                    next, rule.Predicate);
                if (catalogError is not null)
                {
                    return ApplicationResults.Fail(catalogError);
                }
            }

            revision.ReplaceDocument(next, revision.ParentContextHash);
            await policies.SaveRevisionAsync(revision, cancellationToken).ConfigureAwait(false);
            await idempotency.SaveAsync(
                actor, operation, idempotencyKey, requestHash, resourceId, cancellationToken)
                .ConfigureAwait(false);
            await audit.AppendAsync(
                actor,
                operation,
                JsonSerializer.Serialize(new
                {
                    revision_id = revision.Id.Value,
                    content_hash = revision.ContentHash.ToString(),
                    resource_id = resourceId,
                }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(revision, next));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (JsonException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }

    private static async Task<ApplicationResult<PolicyRevisionView>> LoadViewAsync(
        IPolicyStore policies,
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        (PolicyRevision? revision, ApplicationError? loadError) = await PolicyRevisionSupport
            .LoadRevisionAsync(policies, revisionId, cancellationToken)
            .ConfigureAwait(false);
        if (loadError is not null)
        {
            return ApplicationResults.Fail(loadError);
        }

        ApplicationResult<PolicyDocument> document = PolicyRevisionSupport.ReadDocument(revision!);
        if (document.IsFailure)
        {
            return ApplicationResults.Fail(document.Error!);
        }

        return ApplicationResults.Ok(ViewMapper.ToView(revision!, document.Value!));
    }
}
