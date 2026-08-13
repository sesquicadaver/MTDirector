using System.Text.Json;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Writes exact MFC-CJ1 canonical policy revision bytes (Policy Model §33).
/// Property order is schema-fixed; no whitespace; UTF-8 without BOM/trailing newline.
/// </summary>
public static class PolicyCanonicalWriter
{
    public static byte[] Write(PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(PolicyDocument.SchemaName)),
            ("schema_version", w => w.WriteNumber(document.SchemaVersion)),
            ("policy_kind", w => w.WriteString(FormatKind(document.Kind))),
            ("owner_scope", w => w.WriteString(FormatOwnerScope(document.OwnerScope))),
            ("pipeline_version", w => w.WriteString(PolicyPipelineV1.Version)),
            ("chain_contracts", w => WriteChainContracts(w, document.ChainContracts)),
            ("zone_definitions", w => WriteElementArray(w, document.ZoneDefinitions)),
            ("address_objects", w => WriteElementArray(w, document.AddressObjects)),
            ("service_objects", w => WriteElementArray(w, document.ServiceObjects)),
            ("rules", w => WriteRules(w, document.Rules)),
            ("tests", w => WriteElementArray(w, document.Tests)),
            ("exception_metadata", w => w.WriteSortedObject(document.ExceptionMetadata)),
        ]);
        return writer.ToUtf8Bytes();
    }

    public static string FormatKind(PolicyKind kind)
        => kind switch
        {
            PolicyKind.CompanyBaseline => "COMPANY_BASELINE",
            PolicyKind.SiteOverlay => "SITE_OVERLAY",
            PolicyKind.NodeOverlay => "NODE_OVERLAY",
            PolicyKind.Exception => "EXCEPTION",
            _ => throw new DomainInvariantException($"Unknown policy kind '{kind}'."),
        };

    public static string FormatOwnerScope(PolicyOwnerScope scope)
        => scope switch
        {
            PolicyOwnerScope.Company => "COMPANY",
            PolicyOwnerScope.Site => "SITE",
            PolicyOwnerScope.Node => "NODE",
            _ => throw new DomainInvariantException($"Unknown owner scope '{scope}'."),
        };

    public static string FormatRevisionState(PolicyRevisionState state)
        => state switch
        {
            PolicyRevisionState.Draft => "DRAFT",
            PolicyRevisionState.Validated => "VALIDATED",
            PolicyRevisionState.InReview => "IN_REVIEW",
            PolicyRevisionState.Approved => "APPROVED",
            PolicyRevisionState.Rejected => "REJECTED",
            PolicyRevisionState.Superseded => "SUPERSEDED",
            PolicyRevisionState.Revoked => "REVOKED",
            _ => throw new DomainInvariantException($"Unknown revision state '{state}'."),
        };

    private static void WriteChainContracts(CanonicalJsonWriter writer, ChainContractSet contracts)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < contracts.Items.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            WriteChainContract(writer, contracts.Items[i]);
        }

        writer.WriteArrayEnd();
    }

    private static void WriteChainContract(CanonicalJsonWriter writer, ChainContract contract)
    {
        List<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties =
        [
            ("family", w => w.WriteString(PolicyPipelineV1.FormatFamily(contract.Family))),
            ("chain", w => w.WriteString(PolicyPipelineV1.FormatFilterChain(contract.Chain))),
            ("default_disposition", w => w.WriteString(PolicyPipelineV1.FormatDisposition(contract.DefaultDisposition))),
        ];
        if (contract.RejectModeValue is RejectMode mode)
        {
            properties.Add(("reject_mode", w => w.WriteString(PolicyPipelineV1.FormatRejectMode(mode))));
        }

        writer.WriteObject(properties);
    }

    private static void WriteRules(CanonicalJsonWriter writer, IReadOnlyList<PolicyRule> rules)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < rules.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            WriteRule(writer, rules[i]);
        }

        writer.WriteArrayEnd();
    }

    private static void WriteRule(CanonicalJsonWriter writer, PolicyRule rule)
    {
        writer.WriteObject(
        [
            ("id", w => w.WriteString(rule.Id.ToString())),
            ("family", w => w.WriteString(PolicyPipelineV1.FormatFamily(rule.Family))),
            ("chain", w => w.WriteString(PolicyPipelineV1.FormatFilterChain(rule.Chain))),
            ("stage", w => w.WriteString(PolicyPipelineV1.FormatStage(rule.Stage))),
            ("ordinal", w => w.WriteNumber(rule.Ordinal)),
            ("enabled", w => w.WriteBoolean(rule.Enabled)),
            ("predicate", w => WritePredicate(w, rule.Predicate)),
            ("effect", w => WriteEffect(w, rule.Effect)),
            ("logging", w => WriteLogging(w, rule.Logging)),
            ("exception_eligible", w => w.WriteBoolean(rule.ExceptionEligible)),
            ("description", w => w.WriteString(rule.Description)),
        ]);
    }

    private static void WritePredicate(CanonicalJsonWriter writer, TrafficPredicate predicate)
    {
        List<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties = [];
        if (predicate.SourceAddresses is not null)
        {
            properties.Add(("source_addresses", w => WriteAddressSelector(w, predicate.SourceAddresses)));
        }

        if (predicate.DestinationAddresses is not null)
        {
            properties.Add(("destination_addresses", w => WriteAddressSelector(w, predicate.DestinationAddresses)));
        }

        if (predicate.IngressZones is not null)
        {
            properties.Add(("ingress_zones", w => WriteZoneSelector(w, predicate.IngressZones)));
        }

        if (predicate.EgressZones is not null)
        {
            properties.Add(("egress_zones", w => WriteZoneSelector(w, predicate.EgressZones)));
        }

        if (predicate.Services is not null)
        {
            properties.Add(("services", w => WriteServiceSelector(w, predicate.Services)));
        }

        if (predicate.ConnectionStates is not null)
        {
            properties.Add(("connection_states", w => WriteEnumArray(w, predicate.ConnectionStates, FormatConnectionState)));
        }

        if (predicate.ConnectionNatStates is not null)
        {
            properties.Add(("connection_nat_states", w => WriteEnumArray(w, predicate.ConnectionNatStates, FormatConnectionNatState)));
        }

        if (predicate.SourceAddressTypes is not null)
        {
            properties.Add(("source_address_types", w => WriteEnumArray(w, predicate.SourceAddressTypes, FormatAddressType)));
        }

        if (predicate.DestinationAddressTypes is not null)
        {
            properties.Add(("destination_address_types", w => WriteEnumArray(w, predicate.DestinationAddressTypes, FormatAddressType)));
        }

        if (predicate.TcpFlags is not null)
        {
            properties.Add(("tcp_flags", w => WriteTcpFlags(w, predicate.TcpFlags)));
        }

        if (predicate.IpsecPolicy is not null)
        {
            properties.Add(("ipsec_policy", w => WriteIpsec(w, predicate.IpsecPolicy)));
        }

        writer.WriteObject(properties);
    }

    private static void WriteEffect(CanonicalJsonWriter writer, RuleEffectSpec effect)
    {
        List<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties =
        [
            ("kind", w => w.WriteString(PolicyPipelineV1.FormatEffect(effect.Kind))),
        ];
        if (effect.RejectModeValue is RejectMode mode)
        {
            properties.Add(("reject_mode", w => w.WriteString(PolicyPipelineV1.FormatRejectMode(mode))));
        }

        writer.WriteObject(properties);
    }

    private static void WriteLogging(CanonicalJsonWriter writer, LogSpecification logging)
    {
        List<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties =
        [
            ("enabled", w => w.WriteBoolean(logging.Enabled)),
        ];
        if (logging.Prefix is not null)
        {
            properties.Add(("prefix", w => w.WriteString(logging.Prefix)));
        }

        writer.WriteObject(properties);
    }

    private static void WriteAddressSelector(CanonicalJsonWriter writer, AddressSelector selector)
    {
        writer.WriteObject(
        [
            ("include", w => WriteGuidArray(w, selector.Include.Select(static id => id.Value))),
            ("exclude", w => WriteGuidArray(w, selector.Exclude.Select(static id => id.Value))),
        ]);
    }

    private static void WriteZoneSelector(CanonicalJsonWriter writer, ZoneSelector selector)
    {
        writer.WriteObject(
        [
            ("include", w => WriteGuidArray(w, selector.Include.Select(static id => id.Value))),
            ("exclude", w => WriteGuidArray(w, selector.Exclude.Select(static id => id.Value))),
        ]);
    }

    private static void WriteServiceSelector(CanonicalJsonWriter writer, ServiceSelector selector)
    {
        writer.WriteObject(
        [
            ("include", w => WriteGuidArray(w, selector.Include.Select(static id => id.Value))),
        ]);
    }

    private static void WriteTcpFlags(CanonicalJsonWriter writer, TcpFlagConstraint flags)
    {
        writer.WriteObject(
        [
            ("required_present", w => WriteEnumArray(w, flags.RequiredPresent, FormatTcpFlag)),
            ("required_absent", w => WriteEnumArray(w, flags.RequiredAbsent, FormatTcpFlag)),
        ]);
    }

    private static void WriteIpsec(CanonicalJsonWriter writer, IpsecPolicyPredicate ipsec)
    {
        writer.WriteObject(
        [
            ("direction", w => w.WriteString(FormatIpsecDirection(ipsec.Direction))),
            ("policy", w => w.WriteString(FormatIpsecPolicy(ipsec.Policy))),
        ]);
    }

    private static void WriteGuidArray(CanonicalJsonWriter writer, IEnumerable<Guid> ids)
    {
        Guid[] ordered = ids.OrderBy(static g => g).ToArray();
        writer.WriteArrayStart();
        for (int i = 0; i < ordered.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            writer.WriteString(ordered[i].ToString("D"));
        }

        writer.WriteArrayEnd();
    }

    private static void WriteEnumArray<T>(
        CanonicalJsonWriter writer,
        IReadOnlyList<T> values,
        Func<T, string> format)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            writer.WriteString(format(values[i]));
        }

        writer.WriteArrayEnd();
    }

    private static void WriteElementArray(CanonicalJsonWriter writer, IReadOnlyList<JsonElement> elements)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < elements.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            // Opaque elements are re-emitted as compact JSON (no whitespace).
            writer.WriteRaw(elements[i].GetRawText());
        }

        writer.WriteArrayEnd();
    }

    internal static string FormatConnectionState(ConnectionState state)
        => state switch
        {
            ConnectionState.New => "NEW",
            ConnectionState.Established => "ESTABLISHED",
            ConnectionState.Related => "RELATED",
            ConnectionState.Invalid => "INVALID",
            ConnectionState.Untracked => "UNTRACKED",
            _ => throw new DomainInvariantException($"Unknown connection state '{state}'."),
        };

    internal static string FormatConnectionNatState(ConnectionNatState state)
        => state switch
        {
            ConnectionNatState.SrcNat => "SRCNAT",
            ConnectionNatState.DstNat => "DSTNAT",
            _ => throw new DomainInvariantException($"Unknown connection NAT state '{state}'."),
        };

    internal static string FormatAddressType(AddressType type)
        => type switch
        {
            AddressType.Local => "LOCAL",
            AddressType.Unicast => "UNICAST",
            AddressType.Broadcast => "BROADCAST",
            AddressType.Multicast => "MULTICAST",
            AddressType.Anycast => "ANYCAST",
            AddressType.Blackhole => "BLACKHOLE",
            AddressType.Prohibit => "PROHIBIT",
            AddressType.Unreachable => "UNREACHABLE",
            _ => throw new DomainInvariantException($"Unknown address type '{type}'."),
        };

    internal static string FormatTcpFlag(TcpHeaderBit flag)
        => flag switch
        {
            TcpHeaderBit.Fin => "FIN",
            TcpHeaderBit.Syn => "SYN",
            TcpHeaderBit.Rst => "RST",
            TcpHeaderBit.Psh => "PSH",
            TcpHeaderBit.Ack => "ACK",
            TcpHeaderBit.Urg => "URG",
            TcpHeaderBit.Ece => "ECE",
            TcpHeaderBit.Cwr => "CWR",
            _ => throw new DomainInvariantException($"Unknown TCP flag '{flag}'."),
        };

    internal static string FormatIpsecDirection(IpsecDirection direction)
        => direction switch
        {
            IpsecDirection.In => "IN",
            IpsecDirection.Out => "OUT",
            _ => throw new DomainInvariantException($"Unknown IPsec direction '{direction}'."),
        };

    internal static string FormatIpsecPolicy(IpsecPolicyKind policy)
        => policy switch
        {
            IpsecPolicyKind.Ipsec => "IPSEC",
            IpsecPolicyKind.None => "NONE",
            _ => throw new DomainInvariantException($"Unknown IPsec policy '{policy}'."),
        };
}
