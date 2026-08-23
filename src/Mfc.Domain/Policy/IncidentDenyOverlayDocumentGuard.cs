namespace Mfc.Domain.Policy;

/// <summary>
/// Validates INCIDENT_DENY_OVERLAY policy documents against pipeline placement rules (M7.4-01).
/// </summary>
public static class IncidentDenyOverlayDocumentGuard
{
    /// <summary>Validates a document for the incident deny overlay kind and returns a stable code.</summary>
    public static string Validate(PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Kind != PolicyKind.IncidentDenyOverlay)
        {
            return IncidentDenyOverlayCodes.WrongKind;
        }

        if (document.OwnerScope != PolicyOwnerScope.Node)
        {
            return IncidentDenyOverlayCodes.WrongOwnerScope;
        }

        if (document.ExceptionMetadata is not null)
        {
            return IncidentDenyOverlayCodes.ExceptionMetadataForbidden;
        }

        if (document.IncidentDenyOverlayMetadata is null)
        {
            return IncidentDenyOverlayCodes.MetadataRequired;
        }

        if (document.Rules.Count == 0)
        {
            return IncidentDenyOverlayCodes.EmptyRulesForbidden;
        }

        foreach (PolicyRule rule in document.Rules)
        {
            if (rule.Stage != PolicyPipelineStage.IncidentPreStateDeny)
            {
                return IncidentDenyOverlayCodes.StageViolation;
            }

            if (rule.Effect.Kind != PolicyRuleEffect.Drop)
            {
                return IncidentDenyOverlayCodes.EffectViolation;
            }

            if (rule.ExceptionEligible)
            {
                return IncidentDenyOverlayCodes.EffectViolation;
            }
        }

        return IncidentDenyOverlayCodes.ValidDocument;
    }

    /// <summary>Validates and throws when the document is not a conforming incident deny overlay.</summary>
    public static void EnsureValid(PolicyDocument document)
    {
        string code = Validate(document);
        if (code == IncidentDenyOverlayCodes.ValidDocument)
        {
            return;
        }

        throw new DomainInvariantException(
            $"{code}: incident deny overlay document violates pipeline constraints.");
    }

    /// <summary>
    /// Ensures overlay metadata node_id matches the policy container owner when both are known.
    /// </summary>
    public static void EnsureNodeBinding(PolicyDocument document, Guid policyOwnerNodeId)
    {
        EnsureValid(document);
        if (document.IncidentDenyOverlayMetadata!.NodeId != policyOwnerNodeId)
        {
            throw new DomainInvariantException(
                $"{IncidentDenyOverlayCodes.NodeMismatch}: overlay node_id must match policy owner_id.");
        }
    }
}
