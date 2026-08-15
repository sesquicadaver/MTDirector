using System.Globalization;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Typed exception revision metadata (Policy Model §28 / M2-08).</summary>
public sealed class ExceptionMetadata
{
    public PolicyOwnerScope TargetScope { get; }

    public Guid TargetScopeId { get; }

    public PolicyPipelineStage TargetStage { get; }

    public RuleId WaivedRuleId { get; }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset ValidUntil { get; }

    public string Reason { get; }

    public string TicketReference { get; }

    public Guid? SupersedesExceptionId { get; }

    private ExceptionMetadata(
        PolicyOwnerScope targetScope,
        Guid targetScopeId,
        PolicyPipelineStage targetStage,
        RuleId waivedRuleId,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        string reason,
        string ticketReference,
        Guid? supersedesExceptionId)
    {
        TargetScope = targetScope;
        TargetScopeId = targetScopeId;
        TargetStage = targetStage;
        WaivedRuleId = waivedRuleId;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        Reason = reason;
        TicketReference = ticketReference;
        SupersedesExceptionId = supersedesExceptionId;
    }

    /// <summary>Creates metadata and enforces §28.1 finite window + non-empty reason/ticket.</summary>
    public static ExceptionMetadata Create(
        PolicyOwnerScope targetScope,
        Guid targetScopeId,
        PolicyPipelineStage targetStage,
        RuleId waivedRuleId,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        string reason,
        string ticketReference,
        Guid? supersedesExceptionId = null)
    {
        if (targetScope is not (PolicyOwnerScope.Site or PolicyOwnerScope.Node))
        {
            throw new DomainInvariantException("EXCEPTION target_scope must be SITE or NODE.");
        }

        if (targetScopeId == Guid.Empty)
        {
            throw new DomainInvariantException("EXCEPTION target_scope_id must be a concrete UUID.");
        }

        if (targetStage is not (
            PolicyPipelineStage.CompanyDeny or PolicyPipelineStage.SiteDeny or PolicyPipelineStage.NodeDeny))
        {
            throw new DomainInvariantException(
                "EXCEPTION target_stage must be COMPANY_DENY, SITE_DENY, or NODE_DENY.");
        }

        DateTimeOffset from = NormalizeUtc(validFrom);
        DateTimeOffset until = NormalizeUtc(validUntil);
        if (until == DateTimeOffset.MaxValue || until == DateTimeOffset.MinValue)
        {
            throw new DomainInvariantException("EXCEPTION valid_until must be finite.");
        }

        if (until <= from)
        {
            throw new DomainInvariantException("EXCEPTION valid_until must be greater than valid_from.");
        }

        string trimmedReason = RequireNonEmpty(reason, "reason");
        string trimmedTicket = RequireNonEmpty(ticketReference, "ticket_reference");
        return new ExceptionMetadata(
            targetScope,
            targetScopeId,
            targetStage,
            waivedRuleId,
            from,
            until,
            trimmedReason,
            trimmedTicket,
            supersedesExceptionId);
    }

    /// <summary>True when <paramref name="utcNow"/> is at or after <see cref="ValidUntil"/> (inclusive skip).</summary>
    public bool IsExpired(DateTimeOffset utcNow) => NormalizeUtc(utcNow) >= ValidUntil;

    public static string FormatTimestamp(DateTimeOffset value)
        => NormalizeUtc(value).ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseTimestamp(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainInvariantException($"{label} must be a non-empty UTC timestamp.");
        }

        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed))
        {
            throw new DomainInvariantException($"{label} must be a round-trip UTC timestamp.");
        }

        return NormalizeUtc(parsed);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value) => value.ToUniversalTime();

    private static string RequireNonEmpty(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainInvariantException($"EXCEPTION {label} is required.");
        }

        return value.Trim();
    }
}
