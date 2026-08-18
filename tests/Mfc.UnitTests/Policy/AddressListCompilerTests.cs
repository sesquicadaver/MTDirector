using System.Net;
using System.Reflection;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class AddressListCompilerTests
{
    [Fact]
    public void Ac1IncludeExcludeResolveIsExactAgainstResolverAndEncoder()
    {
        AddressObject net = CompanyObj(
            "net",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        AddressObject host = CompanyObj(
            "host",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [net.Id] = net,
            [host.Id] = host,
        };
        AddressSelector selector = AddressSelector.Create([net.Id], [host.Id]);
        AddressSelectorResolveResult resolved = AddressSelectorResolver.Resolve(
            selector,
            IpAddressFamily.IPv4,
            catalog);
        IReadOnlyList<string> expected = AddressPrefixEncoder.Encode(resolved.Intervals);

        AddressListCompileSession session = new();
        AddressListCompileResult result = session.Compile(
            IpAddressFamily.IPv4,
            selector,
            destination: null,
            catalog);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Source);
        Assert.True(result.Source.EmitsMatcher);
        Assert.False(result.Source.Negated);
        Assert.Equal(expected, result.Source.List!.Entries.Select(static e => e.Address).ToArray());
        Assert.DoesNotContain("10.0.0.1", result.Source.List.Entries.Select(static e => e.Address));
        Assert.Contains(result.Source.List.Entries, static e => e.Address == "10.0.0.0");
        Assert.Contains(result.Source.List.Entries, static e => e.Address == "10.0.0.128/25");
    }

    [Fact]
    public void Ac2PositiveSelectorUsesOneListMatcher()
    {
        AddressObject a = CompanyObj(
            "a",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject b = CompanyObj(
            "b",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [a.Id] = a,
            [b.Id] = b,
        };

        AddressListCompileResult result = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([a.Id, b.Id]),
            destination: null,
            catalog);

        Assert.True(result.IsSuccess);
        Assert.Single(result.ReferencedLists);
        Assert.NotNull(result.Source);
        Assert.Equal("src-address-list", result.Source.MatcherKey);
        Assert.Equal(result.ReferencedLists[0].Name, result.Source.MatcherValue);
        Assert.False(result.Source.Negated);
        Assert.Equal(
            ["10.0.0.1", "10.0.0.2"],
            result.ReferencedLists[0].Entries.Select(static e => e.Address).ToArray());
    }

    [Fact]
    public void Ac3UniverseMinusExclusionsUsesNegatedMatcherAndExcludeUnionContent()
    {
        AddressObject deny = CompanyObj(
            "deny",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")),
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [deny.Id] = deny };

        AddressListCompileResult result = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create(include: null, exclude: [deny.Id]),
            destination: null,
            catalog);

        Assert.True(result.IsSuccess);
        Assert.Single(result.ReferencedLists);
        Assert.NotNull(result.Source);
        Assert.True(result.Source.Negated);
        Assert.Equal("src-address-list", result.Source.MatcherKey);
        Assert.Equal("!" + result.ReferencedLists[0].Name, result.Source.MatcherValue);
        Assert.Equal(
            ["10.0.0.1", "10.0.0.2"],
            result.ReferencedLists[0].Entries.Select(static e => e.Address).ToArray());
        Assert.DoesNotContain(result.ReferencedLists[0].Entries, static e => e.Address.Contains('/'));
    }

    [Fact]
    public void Ac4EmptySelectorBlocksCompilationWithoutPartialLists()
    {
        AddressObject net = CompanyObj(
            "net",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [net.Id] = net };
        AddressListCompileSession session = new();

        AddressListCompileResult emptyPositive = session.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([net.Id], [net.Id]),
            destination: null,
            catalog);
        Assert.False(emptyPositive.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.AddressSelectorEmpty, emptyPositive.Code);
        Assert.Empty(emptyPositive.ReferencedLists);
        Assert.Empty(session.InternedLists);

        AddressObject all = CompanyObj(
            "all",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("0.0.0.0"), 0));
        catalog[all.Id] = all;
        AddressListCompileResult emptyUniverse = session.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create(include: null, exclude: [all.Id]),
            destination: null,
            catalog);
        Assert.False(emptyUniverse.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.AddressSelectorEmpty, emptyUniverse.Code);
        Assert.Empty(session.InternedLists);
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(emptyUniverse.Code!));
    }

    [Fact]
    public void Ac4FailedSecondSelectorDoesNotInternFirstDraft()
    {
        AddressObject ok = CompanyObj(
            "ok",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject net = CompanyObj(
            "net",
            AddressEntry.Prefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 8));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [ok.Id] = ok,
            [net.Id] = net,
        };
        AddressListCompileSession session = new();

        AddressListCompileResult result = session.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([ok.Id]),
            AddressSelector.Create([net.Id], [net.Id]),
            catalog);

        Assert.False(result.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.AddressSelectorEmpty, result.Code);
        Assert.Empty(result.ReferencedLists);
        Assert.Empty(session.InternedLists);
    }

    [Fact]
    public void Ac5IdenticalContentReusesSameList()
    {
        AddressObject host = CompanyObj(
            "host",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [host.Id] = host };
        AddressListCompileSession session = new();
        AddressSelector selector = AddressSelector.Create([host.Id]);

        AddressListCompileResult first = session.Compile(IpAddressFamily.IPv4, selector, selector, catalog);
        AddressListCompileResult second = session.Compile(IpAddressFamily.IPv4, selector, null, catalog);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(session.InternedLists);
        Assert.Same(first.Source!.List, first.Destination!.List);
        Assert.Same(first.Source.List, second.Source!.List);
        Assert.Equal(first.Source.MatcherValue, second.Source.MatcherValue);
    }

    [Fact]
    public void Ac6EntriesAreDeterministicAndSortedRegardlessOfInputOrder()
    {
        AddressObject late = CompanyObj(
            "late",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        AddressObject early = CompanyObj(
            "early",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [late.Id] = late,
            [early.Id] = early,
        };

        AddressListCompileResult forward = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([early.Id, late.Id]),
            null,
            catalog);
        AddressListCompileResult reverse = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([late.Id, early.Id]),
            null,
            catalog);

        Assert.Equal(
            ["10.0.0.1", "10.0.0.2"],
            forward.ReferencedLists[0].Entries.Select(static e => e.Address).ToArray());
        Assert.Equal(
            forward.ReferencedLists[0].Entries.Select(static e => e.Address).ToArray(),
            reverse.ReferencedLists[0].Entries.Select(static e => e.Address).ToArray());
        Assert.Equal(forward.ReferencedLists[0].Name, reverse.ReferencedLists[0].Name);
    }

    [Fact]
    public void Ac7TimeoutIsNotUsedOnEntries()
    {
        AddressObject host = CompanyObj(
            "host",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        Dictionary<AddressObjectId, AddressObject> catalog = new() { [host.Id] = host };

        AddressListCompileResult result = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([host.Id]),
            null,
            catalog);

        Assert.True(result.IsSuccess);
        PropertyInfo[] props = typeof(AddressListEntryArtifact).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.Equal(["Address"], props.Select(static p => p.Name).OrderBy(static n => n, StringComparer.Ordinal).ToArray());
        Assert.All(result.ReferencedLists[0].Entries, static e => Assert.DoesNotContain("timeout", e.Address, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac8GeneratedNamesAreBoundedMfcFamilyAContentHash()
    {
        AddressObject v4 = CompanyObj(
            "v4",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject v6 = CompanyObj(
            "v6",
            IpAddressFamily.IPv6,
            AddressEntry.Host(IpAddressFamily.IPv6, IPAddress.Parse("2001:db8::1")));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [v4.Id] = v4,
            [v6.Id] = v6,
        };
        AddressListCompileSession session = new();

        AddressListCompileResult ipv4 = session.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([v4.Id]),
            null,
            catalog);
        AddressListCompileResult ipv6 = session.Compile(
            IpAddressFamily.IPv6,
            AddressSelector.Create([v6.Id]),
            null,
            catalog);

        Assert.Matches("^mfc4\\.a\\.[0-9a-f]{16}$", ipv4.ReferencedLists[0].Name);
        Assert.Matches("^mfc6\\.a\\.[0-9a-f]{16}$", ipv6.ReferencedLists[0].Name);
        Assert.Equal(2, session.InternedLists.Count);
    }

    [Fact]
    public void Ac9SourceAndDestinationUseAtMostOneMatcherEach()
    {
        AddressObject src = CompanyObj(
            "src",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject dst = CompanyObj(
            "dst",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [src.Id] = src,
            [dst.Id] = dst,
        };

        AddressListCompileResult result = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([src.Id]),
            AddressSelector.Create([dst.Id]),
            catalog);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReferencedLists.Count);
        Assert.Equal("src-address-list", result.Source!.MatcherKey);
        Assert.Equal("dst-address-list", result.Destination!.MatcherKey);
        Assert.False(result.Source.Negated);
        Assert.False(result.Destination.Negated);

        AddressListCompileResult universe = new AddressListCompileSession().Compile(
            IpAddressFamily.IPv4,
            source: null,
            destination: AddressSelector.Create(),
            catalog);
        Assert.True(universe.IsSuccess);
        Assert.Empty(universe.ReferencedLists);
        Assert.False(universe.Source!.EmitsMatcher);
        Assert.False(universe.Destination!.EmitsMatcher);
    }

    [Fact]
    public void Ac10ListAndEntryLimitsAreEnforced()
    {
        AddressObject a = CompanyObj(
            "a",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")));
        AddressObject b = CompanyObj(
            "b",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        Dictionary<AddressObjectId, AddressObject> catalog = new()
        {
            [a.Id] = a,
            [b.Id] = b,
        };

        AddressListCompileSession listLimited = new(new AddressListCompileLimits
        {
            MaxLists = 1,
            MaxEntriesPerFamily = AddressListCompileLimits.LayoutV1MaxEntriesPerFamily,
        });
        AddressListCompileResult listFail = listLimited.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([a.Id]),
            AddressSelector.Create([b.Id]),
            catalog);
        Assert.False(listFail.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.AddressListLimitExceeded, listFail.Code);
        Assert.Empty(listFail.ReferencedLists);
        Assert.Empty(listLimited.InternedLists);

        AddressObject wide = CompanyObj(
            "wide",
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.1")),
            AddressEntry.Host(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.2")));
        catalog[wide.Id] = wide;
        AddressListCompileSession entryLimited = new(new AddressListCompileLimits
        {
            MaxLists = AddressListCompileLimits.LayoutV1MaxLists,
            MaxEntriesPerFamily = 1,
        });
        AddressListCompileResult entryFail = entryLimited.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([wide.Id]),
            null,
            catalog);
        Assert.False(entryFail.IsSuccess);
        Assert.Equal(PolicyCompilerCodes.AddressEntryLimitExceeded, entryFail.Code);
        Assert.Empty(entryLimited.InternedLists);

        AddressListCompileSession reuseLimited = new(new AddressListCompileLimits
        {
            MaxLists = 1,
            MaxEntriesPerFamily = AddressListCompileLimits.LayoutV1MaxEntriesPerFamily,
        });
        AddressListCompileResult reuse = reuseLimited.Compile(
            IpAddressFamily.IPv4,
            AddressSelector.Create([a.Id]),
            AddressSelector.Create([a.Id]),
            catalog);
        Assert.True(reuse.IsSuccess);
        Assert.Single(reuseLimited.InternedLists);
        Assert.Same(reuse.Source!.List, reuse.Destination!.List);
    }

    [Fact]
    public void LayoutV1LimitsRejectOutOfRangeCaps()
    {
        Assert.Throws<DomainInvariantException>(() =>
            new AddressListCompileSession(new AddressListCompileLimits
            {
                MaxLists = 0,
                MaxEntriesPerFamily = 1,
            }));
        Assert.Throws<DomainInvariantException>(() =>
            new AddressListCompileSession(new AddressListCompileLimits
            {
                MaxLists = AddressListCompileLimits.LayoutV1MaxLists + 1,
                MaxEntriesPerFamily = 1,
            }));
        Assert.True(PolicyCompilerCodes.IsFailedPrecondition(PolicyCompilerCodes.ResourceNameCollision));
    }

    [Fact]
    public void AddressPrefixEncoderOmitsHostSlashAndEncodesUniverse()
    {
        IReadOnlyList<string> host = AddressPrefixEncoder.Encode(
        [
            new AddressInterval(
                IpAddressFamily.IPv4,
                AddressInterval.ToNumeric(IPAddress.Parse("10.0.0.1"), IpAddressFamily.IPv4),
                AddressInterval.ToNumeric(IPAddress.Parse("10.0.0.1"), IpAddressFamily.IPv4)),
        ]);
        Assert.Equal(["10.0.0.1"], host);

        IReadOnlyList<string> prefix = AddressPrefixEncoder.Encode(
        [
            AddressInterval.FromPrefix(IpAddressFamily.IPv4, IPAddress.Parse("10.0.0.0"), 24),
        ]);
        Assert.Equal(["10.0.0.0/24"], prefix);

        IReadOnlyList<string> universe = AddressPrefixEncoder.Encode([AddressInterval.Universe(IpAddressFamily.IPv4)]);
        Assert.Equal(["0.0.0.0/0"], universe);

        IReadOnlyList<string> v6 = AddressPrefixEncoder.Encode([AddressInterval.Universe(IpAddressFamily.IPv6)]);
        Assert.Equal(["::/0"], v6);
    }

    private static AddressObject CompanyObj(string name, params AddressEntry[] entries)
        => CompanyObj(name, IpAddressFamily.IPv4, entries);

    private static AddressObject CompanyObj(string name, IpAddressFamily family, params AddressEntry[] entries)
        => AddressObject.Create(
            PolicyObjectOwnerScope.Company,
            null,
            null,
            NonEmptyName.Create(name),
            family,
            entries);
}
