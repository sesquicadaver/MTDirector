namespace Mfc.Domain.Incident;

/// <summary>Stable finding and validation codes for incident signal ingress (M7.3-01 / next-2).</summary>
public static class IncidentSignalCodes
{
    public const string MissingEventId = "INCIDENT_SIGNAL_MISSING_EVENT_ID";
    public const string MissingSourceEventId = "INCIDENT_SIGNAL_MISSING_SOURCE_EVENT_ID";
    public const string MissingCategory = "INCIDENT_SIGNAL_MISSING_CATEGORY";
    public const string MissingDeduplicationKey = "INCIDENT_SIGNAL_MISSING_DEDUPLICATION_KEY";
    public const string InvalidSourceType = "INCIDENT_SIGNAL_INVALID_SOURCE_TYPE";
    public const string InvalidSeverity = "INCIDENT_SIGNAL_INVALID_SEVERITY";
    public const string InvalidConfidence = "INCIDENT_SIGNAL_INVALID_CONFIDENCE";
    public const string InvalidOccurredAt = "INCIDENT_SIGNAL_INVALID_OCCURRED_AT";
    public const string InvalidReceivedAt = "INCIDENT_SIGNAL_INVALID_RECEIVED_AT";
    public const string ReceivedBeforeOccurred = "INCIDENT_SIGNAL_RECEIVED_BEFORE_OCCURRED";
    public const string InvalidVlanId = "INCIDENT_SIGNAL_INVALID_VLAN_ID";
    public const string FieldTooLong = "INCIDENT_SIGNAL_FIELD_TOO_LONG";
    public const string InvalidEntityKind = "INCIDENT_SIGNAL_INVALID_ENTITY_KIND";
    public const string MissingEntityValue = "INCIDENT_SIGNAL_MISSING_ENTITY_VALUE";
    public const string EntityValueTooLong = "INCIDENT_SIGNAL_ENTITY_VALUE_TOO_LONG";
    public const string EmptyFlowTuple = "INCIDENT_SIGNAL_EMPTY_FLOW_TUPLE";
    public const string FlowFieldTooLong = "INCIDENT_SIGNAL_FLOW_FIELD_TOO_LONG";
    public const string InvalidFlowPort = "INCIDENT_SIGNAL_INVALID_FLOW_PORT";
    public const string MissingIndicatorType = "INCIDENT_SIGNAL_MISSING_INDICATOR_TYPE";
    public const string MissingIndicatorValue = "INCIDENT_SIGNAL_MISSING_INDICATOR_VALUE";
    public const string IndicatorTypeTooLong = "INCIDENT_SIGNAL_INDICATOR_TYPE_TOO_LONG";
    public const string IndicatorValueTooLong = "INCIDENT_SIGNAL_INDICATOR_VALUE_TOO_LONG";
    public const string RawSyslogRejected = "INCIDENT_SIGNAL_RAW_SYSLOG_REJECTED";
    public const string ForbiddenIngressField = "INCIDENT_SIGNAL_FORBIDDEN_INGRESS_FIELD";
    public const string RouterOsLogRequiresCategory = "INCIDENT_SIGNAL_ROUTEROS_LOG_REQUIRES_CATEGORY";
}
