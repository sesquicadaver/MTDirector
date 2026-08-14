using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Parses MFC-CJ1 policy revision bytes into a typed <see cref="PolicyDocument"/> (LOCK-2 / LOCK-4).
/// Empty <c>rules</c> is allowed; non-empty unparsable rules → <c>POLICY_RULES_UNSUPPORTED_SHAPE</c>.
/// </summary>
public static class PolicyDocumentReader
{
    public const string UnsupportedRulesShapeCode = "POLICY_RULES_UNSUPPORTED_SHAPE";

    public static PolicyDocument Read(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(utf8Json.ToArray());
            return ReadRoot(doc.RootElement);
        }
        catch (DomainInvariantException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException)
        {
            throw new DomainInvariantException(
                $"{UnsupportedRulesShapeCode}: policy document JSON is not a valid MFC-CJ1 payload.",
                ex);
        }
    }

    public static PolicyDocument Read(byte[] utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        return Read(utf8Json.AsSpan());
    }

    private static PolicyDocument ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new DomainInvariantException("Policy document root must be a JSON object.");
        }

        string schema = RequireString(root, "schema");
        if (!string.Equals(schema, PolicyDocument.SchemaName, StringComparison.Ordinal))
        {
            throw new DomainInvariantException($"Unsupported policy schema '{schema}'.");
        }

        uint schemaVersion = RequireUInt32(root, "schema_version");
        PolicyKind kind = ParseKind(RequireString(root, "policy_kind"));
        PolicyOwnerScope ownerScope = ParseOwnerScope(RequireString(root, "owner_scope"));
        _ = RequireString(root, "pipeline_version");

        ChainContractSet chainContracts = ReadChainContracts(root.GetProperty("chain_contracts"), kind);
        IReadOnlyList<JsonElement> zones = CloneArray(root.GetProperty("zone_definitions"));
        IReadOnlyList<JsonElement> addresses = CloneArray(root.GetProperty("address_objects"));
        IReadOnlyList<JsonElement> services = CloneArray(root.GetProperty("service_objects"));
        IReadOnlyList<PolicyRule> rules = ReadRules(root.GetProperty("rules"));
        IReadOnlyList<JsonElement> tests = CloneArray(root.GetProperty("tests"));
        IReadOnlyDictionary<string, string> exceptionMetadata = ReadStringMap(root.GetProperty("exception_metadata"));

        return new PolicyDocument(
            kind,
            ownerScope,
            schemaVersion,
            chainContracts,
            zones,
            addresses,
            services,
            rules,
            tests,
            exceptionMetadata);
    }

    private static List<PolicyRule> ReadRules(JsonElement rulesElement)
    {
        if (rulesElement.ValueKind != JsonValueKind.Array)
        {
            throw UnsupportedRules("rules must be a JSON array.");
        }

        if (rulesElement.GetArrayLength() == 0)
        {
            return [];
        }

        List<PolicyRule> rules = [];
        try
        {
            foreach (JsonElement item in rulesElement.EnumerateArray())
            {
                rules.Add(ReadRule(item));
            }
        }
        catch (DomainInvariantException ex) when (!ex.Message.Contains(UnsupportedRulesShapeCode, StringComparison.Ordinal))
        {
            throw UnsupportedRules(ex.Message, ex);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException)
        {
            throw UnsupportedRules(ex.Message, ex);
        }

        return rules;
    }

    private static PolicyRule ReadRule(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw UnsupportedRules("each rule must be a JSON object.");
        }

        // Reject opaque legacy / unknown top-level matcher surfaces.
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                case "family":
                case "chain":
                case "stage":
                case "ordinal":
                case "enabled":
                case "predicate":
                case "effect":
                case "logging":
                case "exception_eligible":
                case "description":
                    break;
                default:
                    throw UnsupportedRules($"unsupported rule property '{property.Name}'.");
            }
        }

        RuleId id = new(ParseGuid(RequireString(element, "id"), "id"));
        IpAddressFamily family = ParseFamily(RequireString(element, "family"));
        PolicyFilterChain chain = ParseFilterChain(RequireString(element, "chain"));
        PolicyPipelineStage stage = ParseStage(RequireString(element, "stage"));
        uint ordinal = RequireUInt32(element, "ordinal");
        bool enabled = RequireBoolean(element, "enabled");
        TrafficPredicate predicate = ReadPredicate(RequireObject(element, "predicate"));
        RuleEffectSpec effect = ReadEffect(RequireObject(element, "effect"));
        LogSpecification logging = ReadLogging(RequireObject(element, "logging"));
        bool exceptionEligible = RequireBoolean(element, "exception_eligible");
        string description = RequireString(element, "description");

        return PolicyRule.Reconstitute(
            id,
            family,
            chain,
            stage,
            ordinal,
            enabled,
            predicate,
            effect,
            logging,
            exceptionEligible,
            description);
    }

    private static TrafficPredicate ReadPredicate(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "source_addresses":
                case "destination_addresses":
                case "ingress_zones":
                case "egress_zones":
                case "services":
                case "connection_states":
                case "connection_nat_states":
                case "source_address_types":
                case "destination_address_types":
                case "tcp_flags":
                case "ipsec_policy":
                    break;
                default:
                    throw UnsupportedRules($"unsupported predicate property '{property.Name}'.");
            }
        }

        AddressSelector? source = TryGetObject(element, "source_addresses", ReadAddressSelector);
        AddressSelector? destination = TryGetObject(element, "destination_addresses", ReadAddressSelector);
        ZoneSelector? ingress = TryGetObject(element, "ingress_zones", ReadZoneSelector);
        ZoneSelector? egress = TryGetObject(element, "egress_zones", ReadZoneSelector);
        ServiceSelector? services = TryGetObject(element, "services", ReadServiceSelector);
        IReadOnlyList<ConnectionState>? connectionStates = TryGetEnumArray(element, "connection_states", ParseConnectionState);
        IReadOnlyList<ConnectionNatState>? natStates = TryGetEnumArray(element, "connection_nat_states", ParseConnectionNatState);
        IReadOnlyList<AddressType>? srcTypes = TryGetEnumArray(element, "source_address_types", ParseAddressType);
        IReadOnlyList<AddressType>? dstTypes = TryGetEnumArray(element, "destination_address_types", ParseAddressType);
        TcpFlagConstraint? tcpFlags = TryGetObject(element, "tcp_flags", ReadTcpFlags);
        IpsecPolicyPredicate? ipsec = TryGetObject(element, "ipsec_policy", ReadIpsec);

        return TrafficPredicate.Reconstitute(
            source,
            destination,
            ingress,
            egress,
            services,
            connectionStates,
            natStates,
            srcTypes,
            dstTypes,
            tcpFlags,
            ipsec);
    }

    private static RuleEffectSpec ReadEffect(JsonElement element)
    {
        PolicyRuleEffect kind = ParseEffect(RequireString(element, "kind"));
        RejectMode? rejectMode = null;
        if (element.TryGetProperty("reject_mode", out JsonElement modeElement))
        {
            rejectMode = ParseRejectMode(modeElement.GetString() ?? string.Empty);
        }

        return RuleEffectSpec.Create(kind, rejectMode);
    }

    private static LogSpecification ReadLogging(JsonElement element)
    {
        bool enabled = RequireBoolean(element, "enabled");
        string? prefix = null;
        if (element.TryGetProperty("prefix", out JsonElement prefixElement))
        {
            if (prefixElement.ValueKind != JsonValueKind.String)
            {
                throw new DomainInvariantException("logging.prefix must be a string.");
            }

            prefix = prefixElement.GetString();
        }

        return LogSpecification.Create(enabled, prefix);
    }

    private static AddressSelector ReadAddressSelector(JsonElement element)
        => AddressSelector.Create(
            ReadGuidArray(element, "include").Select(static g => new AddressObjectId(g)),
            ReadGuidArray(element, "exclude").Select(static g => new AddressObjectId(g)));

    private static ZoneSelector ReadZoneSelector(JsonElement element)
        => ZoneSelector.Create(
            ReadGuidArray(element, "include").Select(static g => new ZoneId(g)),
            ReadGuidArray(element, "exclude").Select(static g => new ZoneId(g)));

    private static ServiceSelector ReadServiceSelector(JsonElement element)
        => ServiceSelector.Create(
            ReadGuidArray(element, "include").Select(static g => new ServiceObjectId(g)));

    private static TcpFlagConstraint ReadTcpFlags(JsonElement element)
        => TcpFlagConstraint.Create(
            ReadRequiredEnumArray(element, "required_present", ParseTcpFlag),
            ReadRequiredEnumArray(element, "required_absent", ParseTcpFlag));

    private static IpsecPolicyPredicate ReadIpsec(JsonElement element)
        => IpsecPolicyPredicate.Create(
            ParseIpsecDirection(RequireString(element, "direction")),
            ParseIpsecPolicy(RequireString(element, "policy")));

    private static ChainContractSet ReadChainContracts(JsonElement element, PolicyKind kind)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new DomainInvariantException("chain_contracts must be a JSON array.");
        }

        if (kind != PolicyKind.CompanyBaseline)
        {
            if (element.GetArrayLength() != 0)
            {
                throw new DomainInvariantException(
                    $"{PolicyCanonicalWriter.FormatKind(kind)} cannot define chain contracts.");
            }

            return ChainContractSet.ForNonBaseline(kind);
        }

        bool needsMigration = false;
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (ParseDisposition(RequireString(item, "default_disposition"))
                == ChainDefaultDisposition.ReturnToUnmanaged)
            {
                needsMigration = true;
                break;
            }
        }

        PolicyRuntimeMode runtime = needsMigration
            ? PolicyRuntimeMode.MigrationCoexistence
            : PolicyRuntimeMode.ManagedOnly;

        List<ChainContract> contracts = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            contracts.Add(
                ChainContract.Create(
                    ParseFamily(RequireString(item, "family")),
                    ParseFilterChain(RequireString(item, "chain")),
                    ParseDisposition(RequireString(item, "default_disposition")),
                    item.TryGetProperty("reject_mode", out JsonElement modeElement)
                        ? ParseRejectMode(modeElement.GetString() ?? string.Empty)
                        : null,
                    runtime));
        }

        return ChainContractSet.CreateForCompanyBaseline(contracts, runtime);
    }

    private static List<JsonElement> CloneArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new DomainInvariantException("Expected a JSON array.");
        }

        List<JsonElement> items = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            items.Add(item.Clone());
        }

        return items;
    }

    private static Dictionary<string, string> ReadStringMap(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DomainInvariantException("exception_metadata must be a JSON object.");
        }

        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new DomainInvariantException("exception_metadata values must be strings.");
            }

            map[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return map;
    }

    private static Guid[] ReadGuidArray(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new DomainInvariantException($"{name} must be a JSON array.");
        }

        List<Guid> ids = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            ids.Add(ParseGuid(item.GetString() ?? string.Empty, name));
        }

        return ids.ToArray();
    }

    private static List<T>? TryGetEnumArray<T>(
        JsonElement parent,
        string name,
        Func<string, T> parse)
    {
        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            return null;
        }

        return ReadRequiredEnumArray(parent, name, parse);
    }

    private static List<T> ReadRequiredEnumArray<T>(
        JsonElement parent,
        string name,
        Func<string, T> parse)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new DomainInvariantException($"{name} must be a JSON array.");
        }

        List<T> values = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            values.Add(parse(item.GetString() ?? string.Empty));
        }

        return values;
    }

    private static T? TryGetObject<T>(JsonElement parent, string name, Func<JsonElement, T> read)
        where T : class
    {
        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DomainInvariantException($"{name} must be a JSON object.");
        }

        return read(element);
    }

    private static JsonElement RequireObject(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DomainInvariantException($"{name} must be a JSON object.");
        }

        return element;
    }

    private static string RequireString(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new DomainInvariantException($"{name} must be a string.");
        }

        return element.GetString() ?? string.Empty;
    }

    private static uint RequireUInt32(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt32(out uint value))
        {
            throw new DomainInvariantException($"{name} must be an unsigned integer.");
        }

        return value;
    }

    private static bool RequireBoolean(JsonElement parent, string name)
    {
        JsonElement element = parent.GetProperty(name);
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new DomainInvariantException($"{name} must be a boolean."),
        };
    }

    private static Guid ParseGuid(string text, string label)
    {
        if (!Guid.TryParseExact(text, "D", out Guid value) && !Guid.TryParse(text, out value))
        {
            throw new DomainInvariantException($"{label} must be a UUID.");
        }

        return value;
    }

    private static PolicyKind ParseKind(string text)
        => text switch
        {
            "COMPANY_BASELINE" => PolicyKind.CompanyBaseline,
            "SITE_OVERLAY" => PolicyKind.SiteOverlay,
            "NODE_OVERLAY" => PolicyKind.NodeOverlay,
            "EXCEPTION" => PolicyKind.Exception,
            _ => throw new DomainInvariantException($"Unknown policy kind '{text}'."),
        };

    private static PolicyOwnerScope ParseOwnerScope(string text)
        => text switch
        {
            "COMPANY" => PolicyOwnerScope.Company,
            "SITE" => PolicyOwnerScope.Site,
            "NODE" => PolicyOwnerScope.Node,
            _ => throw new DomainInvariantException($"Unknown owner scope '{text}'."),
        };

    private static IpAddressFamily ParseFamily(string text)
        => text switch
        {
            "IPv4" => IpAddressFamily.IPv4,
            "IPv6" => IpAddressFamily.IPv6,
            _ => throw new DomainInvariantException($"Unknown address family '{text}'."),
        };

    private static PolicyFilterChain ParseFilterChain(string text)
        => text switch
        {
            "INPUT" => PolicyFilterChain.Input,
            "FORWARD" => PolicyFilterChain.Forward,
            "OUTPUT" => PolicyFilterChain.Output,
            _ => throw new DomainInvariantException($"Unknown filter chain '{text}'."),
        };

    private static PolicyPipelineStage ParseStage(string text)
        => text switch
        {
            "PROTECTED_CONTROL_PLANE" => PolicyPipelineStage.ProtectedControlPlane,
            "MANDATORY_PRE_STATE_DENY" => PolicyPipelineStage.MandatoryPreStateDeny,
            "STATE_PRELUDE" => PolicyPipelineStage.StatePrelude,
            "COMPANY_DENY_EXEMPTIONS" => PolicyPipelineStage.CompanyDenyExemptions,
            "COMPANY_DENY" => PolicyPipelineStage.CompanyDeny,
            "SITE_DENY_EXEMPTIONS" => PolicyPipelineStage.SiteDenyExemptions,
            "SITE_DENY" => PolicyPipelineStage.SiteDeny,
            "NODE_DENY_EXEMPTIONS" => PolicyPipelineStage.NodeDenyExemptions,
            "NODE_DENY" => PolicyPipelineStage.NodeDeny,
            "COMPANY_ALLOW" => PolicyPipelineStage.CompanyAllow,
            "SITE_ALLOW" => PolicyPipelineStage.SiteAllow,
            "NODE_ALLOW" => PolicyPipelineStage.NodeAllow,
            "DEFAULT_DISPOSITION" => PolicyPipelineStage.DefaultDisposition,
            _ => throw new DomainInvariantException($"Unknown pipeline stage '{text}'."),
        };

    private static PolicyRuleEffect ParseEffect(string text)
        => text switch
        {
            "ACCEPT" => PolicyRuleEffect.Accept,
            "DROP" => PolicyRuleEffect.Drop,
            "REJECT" => PolicyRuleEffect.Reject,
            "FASTTRACK_ACCEPT" => PolicyRuleEffect.FasttrackAccept,
            "EXEMPT_DENY_STAGE" => PolicyRuleEffect.ExemptDenyStage,
            _ => throw new DomainInvariantException($"Unknown rule effect '{text}'."),
        };

    private static RejectMode ParseRejectMode(string text)
        => text switch
        {
            "TCP_RESET" => RejectMode.TcpReset,
            "ADMIN_PROHIBITED" => RejectMode.AdminProhibited,
            "PORT_UNREACHABLE" => RejectMode.PortUnreachable,
            _ => throw new DomainInvariantException($"Unknown reject mode '{text}'."),
        };

    private static ChainDefaultDisposition ParseDisposition(string text)
        => text switch
        {
            "DROP" => ChainDefaultDisposition.Drop,
            "REJECT" => ChainDefaultDisposition.Reject,
            "RETURN_TO_UNMANAGED" => ChainDefaultDisposition.ReturnToUnmanaged,
            _ => throw new DomainInvariantException($"Unknown default disposition '{text}'."),
        };

    private static ConnectionState ParseConnectionState(string text)
        => text switch
        {
            "NEW" => ConnectionState.New,
            "ESTABLISHED" => ConnectionState.Established,
            "RELATED" => ConnectionState.Related,
            "INVALID" => ConnectionState.Invalid,
            "UNTRACKED" => ConnectionState.Untracked,
            _ => throw new DomainInvariantException($"Unknown connection state '{text}'."),
        };

    private static ConnectionNatState ParseConnectionNatState(string text)
        => text switch
        {
            "SRCNAT" => ConnectionNatState.SrcNat,
            "DSTNAT" => ConnectionNatState.DstNat,
            _ => throw new DomainInvariantException($"Unknown connection NAT state '{text}'."),
        };

    private static AddressType ParseAddressType(string text)
        => text switch
        {
            "LOCAL" => AddressType.Local,
            "UNICAST" => AddressType.Unicast,
            "BROADCAST" => AddressType.Broadcast,
            "MULTICAST" => AddressType.Multicast,
            "ANYCAST" => AddressType.Anycast,
            "BLACKHOLE" => AddressType.Blackhole,
            "PROHIBIT" => AddressType.Prohibit,
            "UNREACHABLE" => AddressType.Unreachable,
            _ => throw new DomainInvariantException($"Unknown address type '{text}'."),
        };

    private static TcpHeaderBit ParseTcpFlag(string text)
        => text switch
        {
            "FIN" => TcpHeaderBit.Fin,
            "SYN" => TcpHeaderBit.Syn,
            "RST" => TcpHeaderBit.Rst,
            "PSH" => TcpHeaderBit.Psh,
            "ACK" => TcpHeaderBit.Ack,
            "URG" => TcpHeaderBit.Urg,
            "ECE" => TcpHeaderBit.Ece,
            "CWR" => TcpHeaderBit.Cwr,
            _ => throw new DomainInvariantException($"Unknown TCP flag '{text}'."),
        };

    private static IpsecDirection ParseIpsecDirection(string text)
        => text switch
        {
            "IN" => IpsecDirection.In,
            "OUT" => IpsecDirection.Out,
            _ => throw new DomainInvariantException($"Unknown IPsec direction '{text}'."),
        };

    private static IpsecPolicyKind ParseIpsecPolicy(string text)
        => text switch
        {
            "IPSEC" => IpsecPolicyKind.Ipsec,
            "NONE" => IpsecPolicyKind.None,
            _ => throw new DomainInvariantException($"Unknown IPsec policy '{text}'."),
        };

    private static DomainInvariantException UnsupportedRules(string detail, Exception? inner = null)
        => inner is null
            ? new DomainInvariantException($"{UnsupportedRulesShapeCode}: {detail}")
            : new DomainInvariantException($"{UnsupportedRulesShapeCode}: {detail}", inner);
}
