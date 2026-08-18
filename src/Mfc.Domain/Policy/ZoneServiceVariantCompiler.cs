using System.Globalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Normative zone/service variant limits (Compiler Spec §27, layout v1).</summary>
public sealed class ZoneServiceCompileLimits
{
    public const int LayoutV1MaxInterfaceVariants = 64;

    public const int LayoutV1MaxServiceAtoms = 128;

    public const int LayoutV1MaxPhysicalVariants = 256;

    public const int LayoutV1MaxPortMatcherBytes = 1024;

    public static ZoneServiceCompileLimits LayoutV1 { get; } = new()
    {
        MaxInterfaceVariants = LayoutV1MaxInterfaceVariants,
        MaxServiceAtoms = LayoutV1MaxServiceAtoms,
        MaxPhysicalVariants = LayoutV1MaxPhysicalVariants,
        MaxPortMatcherBytes = LayoutV1MaxPortMatcherBytes,
    };

    public required int MaxInterfaceVariants { get; init; }

    public required int MaxServiceAtoms { get; init; }

    public required int MaxPhysicalVariants { get; init; }

    public required int MaxPortMatcherBytes { get; init; }

    public void EnsureWithinLayoutV1()
    {
        if (MaxInterfaceVariants is < 1 or > LayoutV1MaxInterfaceVariants)
        {
            throw new DomainInvariantException(
                $"MaxInterfaceVariants must be between 1 and {LayoutV1MaxInterfaceVariants} (layout v1).");
        }

        if (MaxServiceAtoms is < 1 or > LayoutV1MaxServiceAtoms)
        {
            throw new DomainInvariantException(
                $"MaxServiceAtoms must be between 1 and {LayoutV1MaxServiceAtoms} (layout v1).");
        }

        if (MaxPhysicalVariants is < 1 or > LayoutV1MaxPhysicalVariants)
        {
            throw new DomainInvariantException(
                $"MaxPhysicalVariants must be between 1 and {LayoutV1MaxPhysicalVariants} (layout v1).");
        }

        if (MaxPortMatcherBytes is < 1 or > LayoutV1MaxPortMatcherBytes)
        {
            throw new DomainInvariantException(
                $"MaxPortMatcherBytes must be between 1 and {LayoutV1MaxPortMatcherBytes} (layout v1).");
        }
    }
}

/// <summary>Per-device compile inputs. Active WAN / running flags are not used (Compiler Spec §25 / §30).</summary>
public sealed class ZoneServiceCompileContext
{
    public required DeviceId DeviceId { get; init; }

    public required IReadOnlyDictionary<ZoneId, NodeZoneBinding> Bindings { get; init; }

    public required ZoneResolveDeviceObservation Observation { get; init; }

    public required IReadOnlyDictionary<ServiceObjectId, ServiceObject> Services { get; init; }

    /// <summary>Ignored operational hint; compilation must not branch on current active WAN.</summary>
    public string? ActiveWanName { get; init; }
}

/// <summary>One physical variant of a logical rule after zone/service expansion (Compiler Spec §14 / §18 / §19).</summary>
public sealed class CompiledPhysicalVariant
{
    public required int VariantIndex { get; init; }

    public required int ServiceAtomIndex { get; init; }

    public required int IngressInterfaceIndex { get; init; }

    public required int EgressInterfaceIndex { get; init; }

    public required int IcmpSelectorIndex { get; init; }

    public required IReadOnlyList<CompiledMatcher> Matchers { get; init; }
}

/// <summary>One RouterOS matcher token emitted by zone/service expansion.</summary>
public sealed class CompiledMatcher
{
    public required string Key { get; init; }

    public required string Value { get; init; }
}

/// <summary>Outcome of compiling zone selectors and a service selector into physical variants.</summary>
public sealed class ZoneServiceCompileResult
{
    private ZoneServiceCompileResult(
        bool isSuccess,
        string? code,
        string? message,
        IReadOnlyList<CompiledPhysicalVariant> variants)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Variants = variants;
    }

    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    public IReadOnlyList<CompiledPhysicalVariant> Variants { get; }

    public static ZoneServiceCompileResult Ok(IReadOnlyList<CompiledPhysicalVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        return new ZoneServiceCompileResult(true, null, null, variants);
    }

    public static ZoneServiceCompileResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ZoneServiceCompileResult(false, code, message, []);
    }
}

/// <summary>
/// Expands logical zone and service unions into bounded physical variants (M3-04).
/// Pure Domain: no RouterOS writes, no connection-state/effect mapping (M3-05), no FastTrack (M3-06).
/// </summary>
public sealed class ZoneServiceVariantCompiler
{
    public ZoneServiceVariantCompiler(ZoneServiceCompileLimits? limits = null)
    {
        Limits = limits ?? ZoneServiceCompileLimits.LayoutV1;
        Limits.EnsureWithinLayoutV1();
    }

    public ZoneServiceCompileLimits Limits { get; }

    public ZoneServiceCompileResult Compile(
        IpAddressFamily family,
        ZoneSelector? ingressZones,
        ZoneSelector? egressZones,
        ServiceSelector? services,
        ZoneServiceCompileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Bindings);
        ArgumentNullException.ThrowIfNull(context.Observation);
        ArgumentNullException.ThrowIfNull(context.Services);
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported compile family '{family}'.");
        }

        _ = context.ActiveWanName;
        ZoneServiceCompileResult? ingress = TryCompileZoneSide(
            ingressZones,
            ZoneMatchRole.Ingress,
            context,
            out ZoneMatchPlan ingressPlan);
        if (ingress is not null)
        {
            return ingress;
        }

        ZoneServiceCompileResult? egress = TryCompileZoneSide(
            egressZones,
            ZoneMatchRole.Egress,
            context,
            out ZoneMatchPlan egressPlan);
        if (egress is not null)
        {
            return egress;
        }

        ZoneServiceCompileResult? atoms = TryCompileServiceAtoms(family, services, context.Services, out IReadOnlyList<ServiceAtom> serviceAtoms);
        if (atoms is not null)
        {
            return atoms;
        }

        return ExpandVariants(ingressPlan, egressPlan, serviceAtoms);
    }

    private ZoneServiceCompileResult? TryCompileZoneSide(
        ZoneSelector? selector,
        ZoneMatchRole role,
        ZoneServiceCompileContext context,
        out ZoneMatchPlan plan)
    {
        if (selector is null || (selector.Include.Count == 0 && selector.Exclude.Count == 0))
        {
            plan = ZoneMatchPlan.Unconstrained(role);
            return null;
        }

        if (selector.Include.Count == 1 && selector.Exclude.Count == 0)
        {
            ZoneId zoneId = selector.Include[0];
            ZoneServiceCompileResult? resolved = ResolveZone(zoneId, context, out ZoneSurface surface);
            if (resolved is not null)
            {
                plan = ZoneMatchPlan.Unconstrained(role);
                return resolved;
            }

            if (surface.DirectInterfaceListName is not null)
            {
                plan = ZoneMatchPlan.InterfaceList(role, surface.DirectInterfaceListName);
                return null;
            }

            return FinishExpanded(role, surface.Interfaces, out plan);
        }

        SortedSet<string> included = new(StringComparer.Ordinal);
        SortedSet<string> excluded = new(StringComparer.Ordinal);
        if (selector.Include.Count == 0)
        {
            foreach (ZoneResolveInterfaceObservation iface in context.Observation.Interfaces)
            {
                if (!iface.Dynamic)
                {
                    included.Add(iface.Name);
                }
            }
        }
        else
        {
            foreach (ZoneId zoneId in selector.Include)
            {
                ZoneServiceCompileResult? resolved = ResolveZone(zoneId, context, out ZoneSurface surface);
                if (resolved is not null)
                {
                    plan = ZoneMatchPlan.Unconstrained(role);
                    return resolved;
                }

                foreach (string name in surface.Interfaces)
                {
                    included.Add(name);
                }
            }
        }

        foreach (ZoneId zoneId in selector.Exclude)
        {
            ZoneServiceCompileResult? resolved = ResolveZone(zoneId, context, out ZoneSurface surface);
            if (resolved is not null)
            {
                plan = ZoneMatchPlan.Unconstrained(role);
                return resolved;
            }

            foreach (string name in surface.Interfaces)
            {
                excluded.Add(name);
            }
        }

        included.ExceptWith(excluded);
        return FinishExpanded(role, included.ToArray(), out plan);
    }

    private ZoneServiceCompileResult? FinishExpanded(
        ZoneMatchRole role,
        IReadOnlyList<string> interfaces,
        out ZoneMatchPlan plan)
    {
        if (interfaces.Count == 0)
        {
            plan = ZoneMatchPlan.Unconstrained(role);
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.ZoneEmpty,
                $"{FormatRole(role)} zone selector resolved to an empty interface set.");
        }

        if (interfaces.Count > Limits.MaxInterfaceVariants)
        {
            plan = ZoneMatchPlan.Unconstrained(role);
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.ZoneExpansionLimit,
                $"{FormatRole(role)} zone expansion would exceed {Limits.MaxInterfaceVariants} interfaces.");
        }

        plan = ZoneMatchPlan.Expanded(role, interfaces);
        return null;
    }

    private static ZoneServiceCompileResult? ResolveZone(
        ZoneId zoneId,
        ZoneServiceCompileContext context,
        out ZoneSurface surface)
    {
        surface = new ZoneSurface([], DirectInterfaceListName: null);
        if (!context.Bindings.TryGetValue(zoneId, out NodeZoneBinding? binding))
        {
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.ZoneNotResolved,
                $"Zone '{zoneId}' has no Node binding on this Device.");
        }

        ZoneBindingResolveResult resolved = ZoneResolveEngine.Resolve(binding, context.Observation);
        if (resolved.Blockers.Count > 0)
        {
            return FailFromResolveBlockers(resolved.Blockers);
        }

        if (resolved.AnalysisStale)
        {
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.CompilerAnalysisStale,
                $"Zone '{zoneId}' binding is stale relative to current interface configuration.");
        }

        if (resolved.ResolvedMembers.Count == 0)
        {
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.ZoneEmpty,
                $"Zone '{zoneId}' resolved to an empty interface set.");
        }

        string? listName = binding.Kind == NodeZoneBindingKind.InterfaceList ? binding.Values[0] : null;
        surface = new ZoneSurface(resolved.ResolvedMembers, listName);
        return null;
    }

    private static ZoneServiceCompileResult FailFromResolveBlockers(IReadOnlyList<ZoneResolveBlocker> blockers)
    {
        if (blockers.Any(static b => b.Code == ZoneResolveBlockerCodes.DynamicInterface))
        {
            ZoneResolveBlocker blocker = blockers.First(static b => b.Code == ZoneResolveBlockerCodes.DynamicInterface);
            return ZoneServiceCompileResult.Fail(PolicyCompilerCodes.ZoneDynamicInterface, blocker.Message);
        }

        if (blockers.Any(static b => b.Code == ZoneResolveBlockerCodes.MissingInterface))
        {
            ZoneResolveBlocker blocker = blockers.First(static b => b.Code == ZoneResolveBlockerCodes.MissingInterface);
            return ZoneServiceCompileResult.Fail(PolicyCompilerCodes.ZoneInterfaceMissing, blocker.Message);
        }

        if (blockers.Any(static b => b.Code == ZoneResolveBlockerCodes.EmptyResolvedSet))
        {
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.ZoneEmpty,
                "Resolved zone interface set is empty.");
        }

        ZoneResolveBlocker first = blockers[0];
        return ZoneServiceCompileResult.Fail(PolicyCompilerCodes.ZoneNotResolved, first.Message);
    }

    private ZoneServiceCompileResult? TryCompileServiceAtoms(
        IpAddressFamily family,
        ServiceSelector? selector,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> catalog,
        out IReadOnlyList<ServiceAtom> atoms)
    {
        if (selector is null || selector.MatchesAnyProtocol)
        {
            atoms = [ServiceAtom.Unconstrained];
            return null;
        }

        ServiceSelectorResolveResult resolved = ServiceSelectorResolver.Resolve(selector, family, catalog);
        List<ServiceAtom> compiled = [];
        foreach (ServiceTerm term in resolved.Terms)
        {
            ZoneServiceCompileResult? encoded = TryEncodeTerm(term, out ServiceAtom atom);
            if (encoded is not null)
            {
                atoms = [];
                return encoded;
            }

            compiled.Add(atom);
        }

        if (compiled.Count > Limits.MaxServiceAtoms)
        {
            atoms = [];
            return ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.RuleVariantLimit,
                $"Service atoms would exceed {Limits.MaxServiceAtoms}.");
        }

        atoms = compiled;
        return null;
    }

    private ZoneServiceCompileResult? TryEncodeTerm(ServiceTerm term, out ServiceAtom atom)
    {
        string? protocol = FormatProtocol(term.Protocol);
        string? srcPorts = EncodePorts(term.SourcePorts, "src-port", out ZoneServiceCompileResult? srcFail);
        if (srcFail is not null)
        {
            atom = ServiceAtom.Unconstrained;
            return srcFail;
        }

        string? dstPorts = EncodePorts(term.DestinationPorts, "dst-port", out ZoneServiceCompileResult? dstFail);
        if (dstFail is not null)
        {
            atom = ServiceAtom.Unconstrained;
            return dstFail;
        }

        IReadOnlyList<IcmpSelector>? icmp = term.IcmpSelectors is { Items.Count: > 0 }
            ? term.IcmpSelectors.Items
            : null;
        atom = new ServiceAtom(protocol, srcPorts, dstPorts, icmp);
        return null;
    }

    private string? EncodePorts(PortSet? ports, string matcher, out ZoneServiceCompileResult? fail)
    {
        fail = null;
        if (ports is null || ports.Intervals.Count == 0)
        {
            return null;
        }

        string encoded = PortMatcherEncoder.Encode(ports);
        if (PortMatcherEncoder.Utf8ByteCount(encoded) > Limits.MaxPortMatcherBytes)
        {
            fail = ZoneServiceCompileResult.Fail(
                PolicyCompilerCodes.ServiceTermTooLarge,
                $"{matcher} encoded size exceeds {Limits.MaxPortMatcherBytes} bytes.");
            return null;
        }

        return encoded;
    }

    private ZoneServiceCompileResult ExpandVariants(
        ZoneMatchPlan ingress,
        ZoneMatchPlan egress,
        IReadOnlyList<ServiceAtom> atoms)
    {
        List<CompiledPhysicalVariant> variants = [];
        int variantIndex = 0;
        for (int serviceIndex = 0; serviceIndex < atoms.Count; serviceIndex++)
        {
            ServiceAtom atom = atoms[serviceIndex];
            IcmpSelector?[] icmpSlots = atom.IcmpSelectors is { Count: > 0 }
                ? atom.IcmpSelectors.Cast<IcmpSelector?>().ToArray()
                : [null];

            for (int ingressIndex = 0; ingressIndex < ingress.Slots.Count; ingressIndex++)
            {
                for (int egressIndex = 0; egressIndex < egress.Slots.Count; egressIndex++)
                {
                    for (int icmpIndex = 0; icmpIndex < icmpSlots.Length; icmpIndex++)
                    {
                        if (variantIndex >= Limits.MaxPhysicalVariants)
                        {
                            return ZoneServiceCompileResult.Fail(
                                PolicyCompilerCodes.RuleVariantLimit,
                                $"Physical variants would exceed {Limits.MaxPhysicalVariants}.");
                        }

                        List<CompiledMatcher> matchers = [];
                        AppendZoneMatcher(matchers, ingress, ingressIndex);
                        AppendZoneMatcher(matchers, egress, egressIndex);
                        if (atom.Protocol is not null)
                        {
                            matchers.Add(new CompiledMatcher { Key = "protocol", Value = atom.Protocol });
                        }

                        if (atom.SourcePorts is not null)
                        {
                            matchers.Add(new CompiledMatcher { Key = "src-port", Value = atom.SourcePorts });
                        }

                        if (atom.DestinationPorts is not null)
                        {
                            matchers.Add(new CompiledMatcher { Key = "dst-port", Value = atom.DestinationPorts });
                        }

                        IcmpSelector? icmp = icmpSlots[icmpIndex];
                        if (icmp is not null)
                        {
                            matchers.Add(new CompiledMatcher
                            {
                                Key = "icmp-options",
                                Value = FormatIcmp(icmp),
                            });
                        }

                        variants.Add(new CompiledPhysicalVariant
                        {
                            VariantIndex = variantIndex,
                            ServiceAtomIndex = serviceIndex,
                            IngressInterfaceIndex = ingressIndex,
                            EgressInterfaceIndex = egressIndex,
                            IcmpSelectorIndex = icmpIndex,
                            Matchers = matchers,
                        });
                        variantIndex++;
                    }
                }
            }
        }

        return ZoneServiceCompileResult.Ok(variants);
    }

    private static void AppendZoneMatcher(List<CompiledMatcher> matchers, ZoneMatchPlan plan, int index)
    {
        ZoneMatchSlot slot = plan.Slots[index];
        if (slot.Key is null || slot.Value is null)
        {
            return;
        }

        matchers.Add(new CompiledMatcher { Key = slot.Key, Value = slot.Value });
    }

    private static string? FormatProtocol(IpProtocol protocol)
    {
        if (protocol.IsAny)
        {
            return null;
        }

        return protocol.CanonicalName is { Length: > 0 } name
            ? name.Trim().ToLowerInvariant()
            : protocol.Number.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatIcmp(IcmpSelector selector)
        => selector.Code is null
            ? selector.Type.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{selector.Type}:{selector.Code.Value}");

    private static string FormatRole(ZoneMatchRole role)
        => role == ZoneMatchRole.Ingress ? "Ingress" : "Egress";

    private enum ZoneMatchRole : byte
    {
        Ingress = 0,
        Egress = 1,
    }

    private sealed record ZoneSurface(IReadOnlyList<string> Interfaces, string? DirectInterfaceListName);

    private readonly record struct ZoneMatchSlot(string? Key, string? Value);

    private sealed class ZoneMatchPlan
    {
        private ZoneMatchPlan(IReadOnlyList<ZoneMatchSlot> slots) => Slots = slots;

        public IReadOnlyList<ZoneMatchSlot> Slots { get; }

        public static ZoneMatchPlan Unconstrained(ZoneMatchRole role)
        {
            _ = role;
            return new ZoneMatchPlan([new ZoneMatchSlot(null, null)]);
        }

        public static ZoneMatchPlan InterfaceList(ZoneMatchRole role, string listName)
        {
            string key = role == ZoneMatchRole.Ingress ? "in-interface-list" : "out-interface-list";
            return new ZoneMatchPlan([new ZoneMatchSlot(key, listName)]);
        }

        public static ZoneMatchPlan Expanded(ZoneMatchRole role, IReadOnlyList<string> interfaces)
        {
            string key = role == ZoneMatchRole.Ingress ? "in-interface" : "out-interface";
            ZoneMatchSlot[] slots = interfaces
                .Select(name => new ZoneMatchSlot(key, name))
                .ToArray();
            return new ZoneMatchPlan(slots);
        }
    }

    private sealed class ServiceAtom
    {
        public static ServiceAtom Unconstrained { get; } = new(null, null, null, null);

        public ServiceAtom(
            string? protocol,
            string? sourcePorts,
            string? destinationPorts,
            IReadOnlyList<IcmpSelector>? icmpSelectors)
        {
            Protocol = protocol;
            SourcePorts = sourcePorts;
            DestinationPorts = destinationPorts;
            IcmpSelectors = icmpSelectors;
        }

        public string? Protocol { get; }

        public string? SourcePorts { get; }

        public string? DestinationPorts { get; }

        public IReadOnlyList<IcmpSelector>? IcmpSelectors { get; }
    }
}
