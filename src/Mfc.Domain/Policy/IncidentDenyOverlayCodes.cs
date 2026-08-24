namespace Mfc.Domain.Policy;

/// <summary>Stable codes for incident deny overlay validation (M7.4-01).</summary>
public static class IncidentDenyOverlayCodes
{
    public const string MetadataRequired = "INCIDENT_DENY_OVERLAY_METADATA_REQUIRED";

    public const string WrongKind = "INCIDENT_DENY_OVERLAY_WRONG_KIND";

    public const string WrongOwnerScope = "INCIDENT_DENY_OVERLAY_WRONG_OWNER_SCOPE";

    public const string StageViolation = "INCIDENT_DENY_OVERLAY_STAGE_VIOLATION";

    public const string EffectViolation = "INCIDENT_DENY_OVERLAY_EFFECT_VIOLATION";

    public const string ExceptionMetadataForbidden = "INCIDENT_DENY_OVERLAY_EXCEPTION_METADATA_FORBIDDEN";

    public const string NodeMismatch = "INCIDENT_DENY_OVERLAY_NODE_MISMATCH";

    public const string EmptyRulesForbidden = "INCIDENT_DENY_OVERLAY_EMPTY_RULES_FORBIDDEN";

    public const string ValidDocument = "INCIDENT_DENY_OVERLAY_VALID";

    public const string RuleUuidCollision = "INCIDENT_DENY_OVERLAY_RULE_UUID_COLLISION";

    public const string BindingRequired = "INCIDENT_DENY_OVERLAY_BINDING_REQUIRED";

    public const string OverlayNotFound = "INCIDENT_DENY_OVERLAY_NOT_FOUND";

    public const string OverlayNodeMismatch = "INCIDENT_DENY_OVERLAY_COMPILE_NODE_MISMATCH";
}
