using System.Text.Json;

namespace Mfc.Domain.Policy;

/// <summary>Frozen <c>POLICY_COMPOSE_*</c> codes (LOCK-11) plus compose INFO findings.</summary>
public static class PolicyComposeCodes
{
    public const string CompanyRequired = "POLICY_COMPOSE_COMPANY_REQUIRED";

    public const string PolicyNotUnique = "POLICY_COMPOSE_POLICY_NOT_UNIQUE";

    public const string ParentContextMismatch = "POLICY_COMPOSE_PARENT_CONTEXT_MISMATCH";

    public const string UuidCollision = "POLICY_COMPOSE_UUID_COLLISION";

    public const string SelectorUnresolved = "POLICY_COMPOSE_SELECTOR_UNRESOLVED";

    public const string Visibility = "POLICY_COMPOSE_VISIBILITY";

    public const string StageOwnership = "POLICY_COMPOSE_STAGE_OWNERSHIP";

    public const string ObjectIdMalformed = "POLICY_COMPOSE_OBJECT_ID_MALFORMED";

    public const string ZoneNotFound = "POLICY_COMPOSE_ZONE_NOT_FOUND";

    public const string UnusedPolicyObject = "UNUSED_POLICY_OBJECT";
}

/// <summary>One loaded policy revision layer for logical compose (company / site / node).</summary>
public sealed class PolicyLayer
{
    public required Guid PolicyId { get; init; }

    public required Guid RevisionId { get; init; }

    public required PolicyKind Kind { get; init; }

    public required PolicyOwnerScope OwnerScope { get; init; }

    public Guid? OwnerId { get; init; }

    public required Inventory.Primitives.Hash256 ContentHash { get; init; }

    public Inventory.Primitives.Hash256? ParentContextHash { get; init; }

    public required PolicyDocument PolicyDocument { get; init; }
}

/// <summary>INFO finding produced by a successful compose (unused objects).</summary>
public sealed class PolicyComposeFinding
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

/// <summary>Ephemeral logical effective policy for a Node (compute-on-read; not persisted).</summary>
public sealed class ComposedEffectivePolicy
{
    public required Inventory.Primitives.Hash256 LogicalEffectiveHash { get; init; }

    public required IReadOnlyList<PolicyRule> ActiveRules { get; init; }

    public required IReadOnlyList<System.Text.Json.JsonElement> MergedAddressObjects { get; init; }

    public required IReadOnlyList<System.Text.Json.JsonElement> MergedServiceObjects { get; init; }

    public required IReadOnlyList<PolicyComposeFinding> Findings { get; init; }
}

/// <summary>Domain compose outcome. Failures use <see cref="Code"/> (<c>POLICY_COMPOSE_*</c>), not <see cref="DomainInvariantException"/>.</summary>
public sealed class PolicyComposeResult
{
    private PolicyComposeResult(bool isSuccess, string? code, string? message, ComposedEffectivePolicy? value)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Value = value;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Frozen <c>POLICY_COMPOSE_*</c> code on failure; null on success.</summary>
    public string? Code { get; }

    public string? Message { get; }

    public ComposedEffectivePolicy? Value { get; }

    public static PolicyComposeResult Ok(ComposedEffectivePolicy value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PolicyComposeResult(true, null, null, value);
    }

    public static PolicyComposeResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new PolicyComposeResult(false, code, message, null);
    }
}

/// <summary>Compose-time catalog entry: identity plus original JSON (M2-07 merge / M2-09 parse).</summary>
public sealed record ComposedPolicyObject(Guid Id, PolicyObjectIdentity Identity, JsonElement Element);
