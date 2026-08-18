using System.Reflection;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ZoneServiceVariantCompilerTests
{
    private static readonly DeviceId Device = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    [Fact]
    public void Ac1ExactInterfaceListBindingIsUsedDirectly()
    {
        ZoneId lan = ZoneId.New();
        ZoneServiceCompileContext context = Context(
            Binding(lan, NodeZoneBindingKind.InterfaceList, ["LAN"], ["ether1", "ether2"]),
            Observation(lists: [List("LAN", include: [], exclude: [])], members: [Member("LAN", "ether1"), Member("LAN", "ether2")]));

        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([lan]),
            egressZones: null,
            services: null,
            context);

        Assert.True(result.IsSuccess);
        CompiledPhysicalVariant variant = Assert.Single(result.Variants);
        CompiledMatcher matcher = Assert.Single(variant.Matchers);
        Assert.Equal("in-interface-list", matcher.Key);
        Assert.Equal("LAN", matcher.Value);
        Assert.DoesNotContain(result.Variants, v => v.Matchers.Any(m => m.Key == "in-interface"));
    }

    [Fact]
    public void Ac2OtherZoneSelectorsExpandToFiniteInterfaces()
    {
        ZoneId lan = ZoneId.New();
        ZoneServiceCompileContext context = Context(
            Binding(lan, NodeZoneBindingKind.ExplicitInterfaceSet, ["ether2", "ether1"], ["ether1", "ether2"]),
            Observation());

        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([lan]),
            null,
            null,
            context);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Variants.Count);
        Assert.Equal("ether1", result.Variants[0].Matchers.Single(m => m.Key == "in-interface").Value);
        Assert.Equal("ether2", result.Variants[1].Matchers.Single(m => m.Key == "in-interface").Value);
        Assert.Equal(0, result.Variants[0].IngressInterfaceIndex);
        Assert.Equal(1, result.Variants[1].IngressInterfaceIndex);
    }

    [Fact]
    public void Ac3IngressEgressCartesianProductIsBounded()
    {
        ZoneId wan = ZoneId.New();
        ZoneId lan = ZoneId.New();
        ZoneServiceCompileContext context = Context(
            [
                Binding(wan, NodeZoneBindingKind.ExplicitInterfaceSet, ["ether1", "ether2"], ["ether1", "ether2"]),
                Binding(lan, NodeZoneBindingKind.ExplicitInterfaceSet, ["ether3"], ["ether3"]),
            ],
            Observation(extra: ["ether3"]),
            catalog: null);

        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([wan]),
            ZoneSelector.Create([lan]),
            null,
            context);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Variants.Count);
        Assert.All(result.Variants, v =>
        {
            Assert.Contains(v.Matchers, m => m.Key == "in-interface");
            Assert.Equal("ether3", v.Matchers.Single(m => m.Key == "out-interface").Value);
        });

        ZoneServiceCompileResult limited = new ZoneServiceVariantCompiler(new ZoneServiceCompileLimits
        {
            MaxInterfaceVariants = 1,
            MaxServiceAtoms = ZoneServiceCompileLimits.LayoutV1MaxServiceAtoms,
            MaxPhysicalVariants = ZoneServiceCompileLimits.LayoutV1MaxPhysicalVariants,
            MaxPortMatcherBytes = ZoneServiceCompileLimits.LayoutV1MaxPortMatcherBytes,
        }).Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([wan]),
            null,
            null,
            context);
        Assert.False(limited.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.ZoneExpansionLimit, limited.Code);
        Assert.Empty(limited.Variants);

        ZoneServiceCompileResult variantLimited = new ZoneServiceVariantCompiler(new ZoneServiceCompileLimits
        {
            MaxInterfaceVariants = ZoneServiceCompileLimits.LayoutV1MaxInterfaceVariants,
            MaxServiceAtoms = ZoneServiceCompileLimits.LayoutV1MaxServiceAtoms,
            MaxPhysicalVariants = 1,
            MaxPortMatcherBytes = ZoneServiceCompileLimits.LayoutV1MaxPortMatcherBytes,
        }).Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([wan]),
            null,
            null,
            context);
        Assert.False(variantLimited.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.RuleVariantLimit, variantLimited.Code);
        Assert.Empty(variantLimited.Variants);
    }

    [Fact]
    public void Ac4ServiceTermsAreCanonicalized()
    {
        ServiceObject http = Service(
            "http",
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Tcp, "tcp"),
                destinationPorts: PortSet.Create([new PortInterval(443, 443), new PortInterval(80, 80)])));
        ServiceObject alsoHttp = Service(
            "also-http",
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Tcp, "tcp"),
                destinationPorts: PortSet.Create([new PortInterval(80, 80)])));
        Dictionary<ServiceObjectId, ServiceObject> catalog = new()
        {
            [http.Id] = http,
            [alsoHttp.Id] = alsoHttp,
        };
        ZoneServiceCompileContext context = Context(catalog: catalog);

        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            null,
            null,
            ServiceSelector.Create([alsoHttp.Id, http.Id]),
            context);

        Assert.True(result.IsSuccess);
        CompiledPhysicalVariant variant = Assert.Single(result.Variants);
        Assert.Equal("tcp", variant.Matchers.Single(m => m.Key == "protocol").Value);
        Assert.Equal("80,443", variant.Matchers.Single(m => m.Key == "dst-port").Value);

        ServiceObject gre = Service(
            "gre",
            ServiceTerm.Create(IpProtocol.Create(47, "gre")));
        catalog[gre.Id] = gre;
        ZoneServiceCompileResult numericOrder = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            null,
            null,
            ServiceSelector.Create([gre.Id, http.Id]),
            Context(catalog: catalog));
        Assert.True(numericOrder.IsSuccess);
        Assert.Equal(2, numericOrder.Variants.Count);
        Assert.Equal("tcp", numericOrder.Variants[0].Matchers.Single(m => m.Key == "protocol").Value);
        Assert.Equal("gre", numericOrder.Variants[1].Matchers.Single(m => m.Key == "protocol").Value);
    }

    [Fact]
    public void Ac5IcmpSelectorsCreateSeparateVariants()
    {
        ServiceObject icmp = Service(
            "icmp",
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Icmp, "icmp"),
                icmpSelectors: IcmpSelectorSet.Create([new IcmpSelector(8), new IcmpSelector(0, 0)])));
        ZoneServiceCompileContext context = Context(catalog: new Dictionary<ServiceObjectId, ServiceObject> { [icmp.Id] = icmp });

        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            null,
            null,
            ServiceSelector.Create([icmp.Id]),
            context);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Variants.Count);
        Assert.Equal("0:0", result.Variants[0].Matchers.Single(m => m.Key == "icmp-options").Value);
        Assert.Equal("8", result.Variants[1].Matchers.Single(m => m.Key == "icmp-options").Value);
        Assert.Equal(0, result.Variants[0].IcmpSelectorIndex);
        Assert.Equal(1, result.Variants[1].IcmpSelectorIndex);
        Assert.All(result.Variants, v => Assert.Equal("icmp", v.Matchers.Single(m => m.Key == "protocol").Value));
    }

    [Fact]
    public void Ac6PortMatcherHasBoundedEncodedSize()
    {
        ServiceObject wide = Service(
            "wide",
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Tcp, "tcp"),
                destinationPorts: PortSet.Create([new PortInterval(1, 100)])));
        ZoneServiceCompileContext context = Context(catalog: new Dictionary<ServiceObjectId, ServiceObject> { [wide.Id] = wide });

        ZoneServiceCompileResult fail = new ZoneServiceVariantCompiler(new ZoneServiceCompileLimits
        {
            MaxInterfaceVariants = ZoneServiceCompileLimits.LayoutV1MaxInterfaceVariants,
            MaxServiceAtoms = ZoneServiceCompileLimits.LayoutV1MaxServiceAtoms,
            MaxPhysicalVariants = ZoneServiceCompileLimits.LayoutV1MaxPhysicalVariants,
            MaxPortMatcherBytes = 4,
        }).Compile(
            IpAddressFamily.IPv4,
            null,
            null,
            ServiceSelector.Create([wide.Id]),
            context);

        Assert.False(fail.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.ServiceTermTooLarge, fail.Code);
        Assert.Empty(fail.Variants);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(fail.Code!));

        string encoded = PortMatcherEncoder.Encode(PortSet.Create([new PortInterval(443, 443), new PortInterval(80, 80)]));
        Assert.Equal("80,443", encoded);
        Assert.Equal(6, PortMatcherEncoder.Utf8ByteCount(encoded));
    }

    [Fact]
    public void Ac7VariantOrderIsDeterministic()
    {
        ZoneId lan = ZoneId.New();
        ServiceObject mixed = Service(
            "mixed",
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Udp, "udp"),
                destinationPorts: PortSet.Create([new PortInterval(53, 53)])),
            ServiceTerm.Create(
                IpProtocol.Create(IpProtocol.Tcp, "tcp"),
                destinationPorts: PortSet.Create([new PortInterval(443, 443)])));
        ZoneServiceCompileContext context = Context(
            Binding(lan, NodeZoneBindingKind.ExplicitInterfaceSet, ["ether2", "ether1"], ["ether1", "ether2"]),
            Observation(),
            catalog: new Dictionary<ServiceObjectId, ServiceObject> { [mixed.Id] = mixed });

        ZoneServiceCompileResult a = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([lan]),
            null,
            ServiceSelector.Create([mixed.Id]),
            context);
        ZoneServiceCompileResult b = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([lan]),
            null,
            ServiceSelector.Create([mixed.Id]),
            context);

        Assert.True(a.IsSuccess);
        Assert.Equal(4, a.Variants.Count);
        Assert.Equal(
            a.Variants.Select(FormatVariant).ToArray(),
            b.Variants.Select(FormatVariant).ToArray());
        Assert.Equal(0, a.Variants[0].ServiceAtomIndex);
        Assert.Equal("tcp", a.Variants[0].Matchers.Single(m => m.Key == "protocol").Value);
        Assert.Equal("ether1", a.Variants[0].Matchers.Single(m => m.Key == "in-interface").Value);
        Assert.Equal("ether2", a.Variants[1].Matchers.Single(m => m.Key == "in-interface").Value);
        Assert.Equal("udp", a.Variants[2].Matchers.Single(m => m.Key == "protocol").Value);
        Assert.Equal(Enumerable.Range(0, 4), a.Variants.Select(v => v.VariantIndex));
    }

    [Fact]
    public void Ac8InterfaceRunningStateIsNotACompileInput()
    {
        PropertyInfo[] props = typeof(ZoneResolveInterfaceObservation).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Equal(
            ["Dynamic", "Name"],
            props.Select(static p => p.Name).OrderBy(static n => n, StringComparer.Ordinal).ToArray());

        ZoneId lan = ZoneId.New();
        ZoneServiceCompileContext context = Context(
            Binding(lan, NodeZoneBindingKind.SingleInterface, ["ether1"], ["ether1"]),
            Observation());
        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([lan]),
            null,
            null,
            context);
        Assert.True(result.IsSuccess);
        Assert.Equal("ether1", Assert.Single(result.Variants).Matchers.Single().Value);
    }

    [Fact]
    public void Ac9CurrentActiveWanDoesNotChangeVariants()
    {
        ZoneId wan = ZoneId.New();
        (NodeZoneBinding binding, ZoneResolveDeviceObservation observation) = WanBinding(wan);
        ZoneServiceCompileContext primary = new()
        {
            DeviceId = Device,
            Bindings = new Dictionary<ZoneId, NodeZoneBinding> { [wan] = binding },
            Observation = observation,
            Services = new Dictionary<ServiceObjectId, ServiceObject>(),
            ActiveWanName = "ether1",
        };
        ZoneServiceCompileContext backup = new()
        {
            DeviceId = Device,
            Bindings = new Dictionary<ZoneId, NodeZoneBinding> { [wan] = binding },
            Observation = observation,
            Services = new Dictionary<ServiceObjectId, ServiceObject>(),
            ActiveWanName = "ether2",
        };

        ZoneServiceCompileResult a = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([wan]),
            null,
            null,
            primary);
        ZoneServiceCompileResult b = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([wan]),
            null,
            null,
            backup);

        Assert.True(a.IsSuccess);
        Assert.Equal(2, a.Variants.Count);
        Assert.Equal(
            a.Variants.Select(FormatVariant).ToArray(),
            b.Variants.Select(FormatVariant).ToArray());
        Assert.Contains(a.Variants, v => v.Matchers.Any(m => m.Value == "ether1"));
        Assert.Contains(a.Variants, v => v.Matchers.Any(m => m.Value == "ether2"));
    }

    [Fact]
    public void Ac10EmptyOrStaleZoneBlocksCompilation()
    {
        ZoneId missing = ZoneId.New();
        ZoneServiceCompileResult unresolved = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([missing]),
            null,
            null,
            Context());
        Assert.False(unresolved.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.ZoneNotResolved, unresolved.Code);
        Assert.Empty(unresolved.Variants);

        ZoneId empty = ZoneId.New();
        NodeZoneBinding emptyBinding = Binding(
            empty,
            NodeZoneBindingKind.SingleInterface,
            ["missing0"],
            resolvedMembers: []);
        ZoneServiceCompileResult emptyResult = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([empty]),
            null,
            null,
            Context(emptyBinding, Observation()));
        Assert.False(emptyResult.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.ZoneInterfaceMissing, emptyResult.Code);
        Assert.Empty(emptyResult.Variants);

        ZoneId stale = ZoneId.New();
        NodeZoneBinding staleBinding = NodeZoneBinding.Create(
            new NodeId(Guid.NewGuid()),
            stale,
            NodeZoneBindingKind.SingleInterface,
            ["ether1"],
            Hash256.Create(new byte[32]));
        ZoneServiceCompileResult staleResult = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([stale]),
            null,
            null,
            Context(staleBinding, Observation()));
        Assert.False(staleResult.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.CompilerAnalysisStale, staleResult.Code);
        Assert.Empty(staleResult.Variants);

        ZoneId dynamic = ZoneId.New();
        NodeZoneBinding dynamicBinding = Binding(
            dynamic,
            NodeZoneBindingKind.SingleInterface,
            ["pppoe-out1"],
            resolvedMembers: []);
        ZoneResolveDeviceObservation dynamicObs = Observation(extra: ["pppoe-out1"], dynamicNames: ["pppoe-out1"]);
        ZoneServiceCompileResult dynamicResult = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            ZoneSelector.Create([dynamic]),
            null,
            null,
            Context(dynamicBinding, dynamicObs));
        Assert.False(dynamicResult.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.ZoneDynamicInterface, dynamicResult.Code);
    }

    [Fact]
    public void UnconstrainedSelectorsEmitSingleEmptyMatcherVariant()
    {
        ZoneServiceCompileResult result = new ZoneServiceVariantCompiler().Compile(
            IpAddressFamily.IPv4,
            null,
            ZoneSelector.Create(),
            ServiceSelector.Create(),
            Context());
        Assert.True(result.IsSuccess);
        CompiledPhysicalVariant variant = Assert.Single(result.Variants);
        Assert.Empty(variant.Matchers);
        Assert.Equal(0, variant.VariantIndex);
    }

    private static string FormatVariant(CompiledPhysicalVariant variant)
        => string.Join(
            '|',
            variant.Matchers.Select(m => $"{m.Key}={m.Value}"));

    private static ZoneServiceCompileContext Context(
        params NodeZoneBinding[] bindings)
        => Context(bindings, Observation(), catalog: null);

    private static ZoneServiceCompileContext Context(
        NodeZoneBinding binding,
        ZoneResolveDeviceObservation observation)
        => Context([binding], observation, catalog: null);

    private static ZoneServiceCompileContext Context(
        NodeZoneBinding binding,
        ZoneResolveDeviceObservation observation,
        Dictionary<ServiceObjectId, ServiceObject> catalog)
        => Context([binding], observation, catalog);

    private static ZoneServiceCompileContext Context(
        IReadOnlyList<NodeZoneBinding> bindings,
        ZoneResolveDeviceObservation observation,
        Dictionary<ServiceObjectId, ServiceObject>? catalog)
        => new()
        {
            DeviceId = Device,
            Bindings = bindings.ToDictionary(static b => b.ZoneId),
            Observation = observation,
            Services = catalog ?? new Dictionary<ServiceObjectId, ServiceObject>(),
        };

    private static ZoneServiceCompileContext Context(Dictionary<ServiceObjectId, ServiceObject> catalog)
        => Context([], Observation(), catalog);

    private static (NodeZoneBinding Binding, ZoneResolveDeviceObservation Observation) WanBinding(ZoneId wan)
    {
        NodeZoneBinding binding = Binding(
            wan,
            NodeZoneBindingKind.ExplicitInterfaceSet,
            ["ether1", "ether2"],
            ["ether1", "ether2"]);
        return (binding, Observation());
    }

    private static NodeZoneBinding Binding(
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        IReadOnlyList<string> resolvedMembers)
    {
        Hash256 expected = NodeZoneBinding.ComputeDependencyHash(kind, values, resolvedMembers);
        return NodeZoneBinding.Create(new NodeId(Guid.NewGuid()), zoneId, kind, values, expected);
    }

    private static ZoneResolveDeviceObservation Observation(
        IReadOnlyList<InterfaceListSpec>? lists = null,
        IReadOnlyList<InterfaceListMemberSpec>? members = null,
        IReadOnlyList<string>? extra = null,
        IReadOnlyList<string>? dynamicNames = null)
    {
        HashSet<string> names = new(StringComparer.Ordinal) { "ether1", "ether2" };
        if (extra is not null)
        {
            foreach (string name in extra)
            {
                names.Add(name);
            }
        }

        HashSet<string> dynamic = new(dynamicNames ?? [], StringComparer.Ordinal);
        return new ZoneResolveDeviceObservation
        {
            DeviceId = Device,
            ObservationAvailable = true,
            Interfaces = names
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => new ZoneResolveInterfaceObservation { Name = n, Dynamic = dynamic.Contains(n) })
                .ToArray(),
            InterfaceLists = lists ?? [],
            InterfaceListMembers = members ?? [],
        };
    }

    private static InterfaceListSpec List(string name, IReadOnlyList<string> include, IReadOnlyList<string> exclude)
        => new()
        {
            Name = name,
            Include = include,
            Exclude = exclude,
        };

    private static InterfaceListMemberSpec Member(string list, string iface)
        => new()
        {
            List = list,
            Interface = iface,
            Disabled = false,
        };

    private static ServiceObject Service(string name, params ServiceTerm[] terms)
        => ServiceObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            terms);
}
