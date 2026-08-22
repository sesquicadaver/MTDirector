using System.Text.RegularExpressions;

namespace Mfc.Domain.Incident;

/// <summary>
/// Fail-closed ingress guard for normalized incident signals (M7.3-01).
/// Rejects raw syslog payloads and alternate ingress field names; Controller stores no raw syslog.
/// </summary>
public static class IncidentSignalIngressGuard
{
    private static readonly Regex SyslogPriorityPrefix = new(@"^<\d{1,3}>", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RouterOsTopicPrefix = new(
        @"^(debug|info|warning|error|critical|firewall|system|account|wireless|dhcp|ppp|ipsec|ovpn|container)\s",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Field names that must never appear on ingress envelopes (raw syslog bypass).</summary>
    public static IReadOnlyList<string> ForbiddenIngressFieldNames { get; } =
    [
        "raw_syslog",
        "syslog_message",
        "syslog_payload",
        "message_body",
        "routeros_log_line",
        "unparsed_log",
    ];

    /// <summary>Rejects alternate ingress field names that would carry raw syslog inline.</summary>
    public static void RejectForbiddenIngressFieldNames(IEnumerable<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);
        foreach (string fieldName in fieldNames)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                continue;
            }

            string normalized = fieldName.Trim();
            foreach (string forbidden in ForbiddenIngressFieldNames)
            {
                if (string.Equals(normalized, forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    throw new DomainInvariantException(
                        $"{IncidentSignalCodes.ForbiddenIngressField}: ingress field '{normalized}' is not allowed.");
                }
            }
        }
    }

    /// <summary>Rejects inline syslog bodies masquerading as references or evidence.</summary>
    public static void RejectInlineRawSyslog(string? rawEventRef, IReadOnlyList<string>? evidenceRefs)
    {
        if (!string.IsNullOrWhiteSpace(rawEventRef) && LooksLikeInlineSyslog(rawEventRef))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.RawSyslogRejected}: raw_event_ref must be an external reference, not inline syslog.");
        }

        if (evidenceRefs is null)
        {
            return;
        }

        foreach (string evidenceRef in evidenceRefs)
        {
            if (LooksLikeInlineSyslog(evidenceRef))
            {
                throw new DomainInvariantException(
                    $"{IncidentSignalCodes.RawSyslogRejected}: evidence_refs must not contain inline syslog payloads.");
            }
        }
    }

    /// <summary>
    /// ROUTEROS_LOG events must already be normalized into a non-generic category before ingress.
    /// </summary>
    public static void EnsureRouterOsLogCategory(IncidentSignalSourceType sourceType, string category)
    {
        if (sourceType != IncidentSignalSourceType.RouterOsLog)
        {
            return;
        }

        string normalized = category.Trim();
        if (normalized.Length == 0
            || string.Equals(normalized, "syslog", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "routeros_log", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "log", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainInvariantException(
                $"{IncidentSignalCodes.RouterOsLogRequiresCategory}: ROUTEROS_LOG signals require a normalized category.");
        }
    }

    internal static bool LooksLikeInlineSyslog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Contains('\n', StringComparison.Ordinal) || trimmed.Contains('\r', StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Length > 512 && SyslogPriorityPrefix.IsMatch(trimmed))
        {
            return true;
        }

        if (SyslogPriorityPrefix.IsMatch(trimmed))
        {
            return true;
        }

        if (RouterOsTopicPrefix.IsMatch(trimmed) && trimmed.Contains("message=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.Contains("facility=", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("severity=", StringComparison.OrdinalIgnoreCase);
    }
}
