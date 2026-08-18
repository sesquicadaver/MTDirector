using System.Collections.Immutable;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>Normative compile limits for address lists (Compiler Spec §27, layout v1).</summary>
public sealed class AddressListCompileLimits
{
    public const int LayoutV1MaxLists = 4096;

    public const int LayoutV1MaxEntriesPerFamily = 250_000;

    public static AddressListCompileLimits LayoutV1 { get; } = new()
    {
        MaxLists = LayoutV1MaxLists,
        MaxEntriesPerFamily = LayoutV1MaxEntriesPerFamily,
    };

    public required int MaxLists { get; init; }

    public required int MaxEntriesPerFamily { get; init; }

    /// <summary>Validates limits are positive and do not exceed layout v1 caps (Compiler Spec §27).</summary>
    public void EnsureWithinLayoutV1()
    {
        if (MaxLists is < 1 or > LayoutV1MaxLists)
        {
            throw new DomainInvariantException(
                $"MaxLists must be between 1 and {LayoutV1MaxLists} (layout v1).");
        }

        if (MaxEntriesPerFamily is < 1 or > LayoutV1MaxEntriesPerFamily)
        {
            throw new DomainInvariantException(
                $"MaxEntriesPerFamily must be between 1 and {LayoutV1MaxEntriesPerFamily} (layout v1).");
        }
    }
}

/// <summary>Source vs destination list matcher (Compiler Spec §15 / §16).</summary>
public enum AddressListMatcherRole : byte
{
    Source = 0,
    Destination = 1,
}

/// <summary>One compiled address selector: omitted matcher, or a single list matcher.</summary>
public sealed class CompiledAddressSelector
{
    public required AddressListMatcherRole Role { get; init; }

    /// <summary>False when the selector is unconstrained universe (Compiler Spec §16.1).</summary>
    public bool EmitsMatcher { get; init; }

    public bool Negated { get; init; }

    public string? MatcherKey { get; init; }

    public string? MatcherValue { get; init; }

    public AddressListArtifactDraft? List { get; init; }
}

/// <summary>Outcome of compiling one or two address selectors (no partial lists on failure).</summary>
public sealed class AddressListCompileResult
{
    private AddressListCompileResult(
        bool isSuccess,
        string? code,
        string? message,
        CompiledAddressSelector? source,
        CompiledAddressSelector? destination,
        IReadOnlyList<AddressListArtifactDraft> lists)
    {
        IsSuccess = isSuccess;
        Code = code;
        Message = message;
        Source = source;
        Destination = destination;
        ReferencedLists = lists;
    }

    public bool IsSuccess { get; }

    public string? Code { get; }

    public string? Message { get; }

    public CompiledAddressSelector? Source { get; }

    public CompiledAddressSelector? Destination { get; }

    /// <summary>
    /// Lists referenced by this compile call's matchers (0–2 drafts). Distinct from
    /// <see cref="AddressListCompileSession.InternedLists"/>, which is the full intern pool.
    /// </summary>
    public IReadOnlyList<AddressListArtifactDraft> ReferencedLists { get; }

    public static AddressListCompileResult Ok(
        CompiledAddressSelector? source,
        CompiledAddressSelector? destination,
        IReadOnlyList<AddressListArtifactDraft> lists)
    {
        ArgumentNullException.ThrowIfNull(lists);
        return new AddressListCompileResult(true, null, null, source, destination, lists);
    }

    public static AddressListCompileResult Fail(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new AddressListCompileResult(false, code, message, null, null, []);
    }
}

/// <summary>
/// Compiles address selectors into interned content-addressed RouterOS lists (Compiler Spec §8.4 / §16, M3-03).
/// Pure Domain: no RouterOS writes, no timeout fields, at most one src and one dst list matcher.
/// </summary>
public sealed class AddressListCompileSession
{
    private readonly Dictionary<string, AddressListArtifactDraft> _listsByContentHash = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _contentHashByName = new(StringComparer.Ordinal);

    private readonly Dictionary<IpAddressFamily, int> _entryCountByFamily = [];

    public AddressListCompileSession(AddressListCompileLimits? limits = null)
    {
        Limits = limits ?? AddressListCompileLimits.LayoutV1;
        Limits.EnsureWithinLayoutV1();
    }

    public AddressListCompileLimits Limits { get; }

    /// <summary>Full intern pool for the artifact payload (all committed lists).</summary>
    public IReadOnlyList<AddressListArtifactDraft> InternedLists
        => _listsByContentHash.Values
            .OrderBy(static l => RouterOsFilterArtifactIdentity.FormatFamily(l.Family), StringComparer.Ordinal)
            .ThenBy(static l => l.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Compiles optional source and destination selectors for one rule family.</summary>
    public AddressListCompileResult Compile(
        IpAddressFamily family,
        AddressSelector? source,
        AddressSelector? destination,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported compile family '{family}'.");
        }

        Dictionary<string, AddressListArtifactDraft> pending = new(StringComparer.Ordinal);
        AddressListCompileResult? sourceResult = TryCompileSelector(
            family,
            source,
            AddressListMatcherRole.Source,
            catalog,
            pending,
            out CompiledAddressSelector? sourceMatch);
        if (sourceResult is not null)
        {
            return sourceResult;
        }

        AddressListCompileResult? destResult = TryCompileSelector(
            family,
            destination,
            AddressListMatcherRole.Destination,
            catalog,
            pending,
            out CompiledAddressSelector? destMatch);
        if (destResult is not null)
        {
            return destResult;
        }

        Commit(pending);
        return AddressListCompileResult.Ok(sourceMatch, destMatch, CollectReferencedLists(sourceMatch, destMatch));
    }

    private static AddressListArtifactDraft[] CollectReferencedLists(
        CompiledAddressSelector? source,
        CompiledAddressSelector? destination)
    {
        List<AddressListArtifactDraft> lists = [];
        if (source?.List is not null)
        {
            lists.Add(source.List);
        }

        if (destination?.List is not null
            && !ReferenceEquals(destination.List, source?.List))
        {
            lists.Add(destination.List);
        }

        return lists
            .OrderBy(static l => RouterOsFilterArtifactIdentity.FormatFamily(l.Family), StringComparer.Ordinal)
            .ThenBy(static l => l.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private AddressListCompileResult? TryCompileSelector(
        IpAddressFamily family,
        AddressSelector? selector,
        AddressListMatcherRole role,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog,
        Dictionary<string, AddressListArtifactDraft> pending,
        out CompiledAddressSelector match)
    {
        if (selector is null || (selector.Include.Count == 0 && selector.Exclude.Count == 0))
        {
            match = Universe(role);
            return null;
        }

        if (selector.Include.Count == 0)
        {
            return TryCompileUniverseMinusExclusions(family, selector, role, catalog, pending, out match);
        }

        AddressSelectorResolveResult resolved = AddressSelectorResolver.Resolve(selector, family, catalog);
        if (resolved.IsUnsatisfiable)
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.AddressSelectorEmpty,
                $"{FormatRole(role)} address selector resolved to an empty set.");
        }

        return InternList(family, resolved.Intervals, negated: false, role, pending, out match);
    }

    private AddressListCompileResult? TryCompileUniverseMinusExclusions(
        IpAddressFamily family,
        AddressSelector selector,
        AddressListMatcherRole role,
        IReadOnlyDictionary<AddressObjectId, AddressObject> catalog,
        Dictionary<string, AddressListArtifactDraft> pending,
        out CompiledAddressSelector match)
    {
        AddressSelectorResolveResult effective = AddressSelectorResolver.Resolve(selector, family, catalog);
        if (effective.IsUnsatisfiable)
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.AddressSelectorEmpty,
                $"{FormatRole(role)} universe-minus-exclusions resolved to an empty set.");
        }

        AddressSelectorResolveResult excluded = AddressSelectorResolver.Resolve(
            AddressSelector.Create(include: selector.Exclude, exclude: null),
            family,
            catalog);
        if (excluded.IsUnsatisfiable)
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.AddressSelectorEmpty,
                $"{FormatRole(role)} exclusion set is empty.");
        }

        return InternList(family, excluded.Intervals, negated: true, role, pending, out match);
    }

    private AddressListCompileResult? InternList(
        IpAddressFamily family,
        IReadOnlyList<AddressInterval> intervals,
        bool negated,
        AddressListMatcherRole role,
        Dictionary<string, AddressListArtifactDraft> pending,
        out CompiledAddressSelector match)
    {
        IReadOnlyList<string> encoded = AddressPrefixEncoder.Encode(intervals);
        if (encoded.Count == 0)
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.AddressSelectorEmpty,
                $"{FormatRole(role)} address selector produced no list entries.");
        }

        List<AddressListEntryArtifact> entries = new(encoded.Count);
        foreach (string address in encoded)
        {
            entries.Add(AddressListEntryArtifact.Create(address));
        }

        ImmutableArray<AddressListEntryArtifact> sealedEntries = entries
            .OrderBy(static e => e.Address, StringComparer.Ordinal)
            .ToImmutableArray();
        Hash256 contentHash = RouterOsFilterArtifactIdentity.HashAddressListContent(family, sealedEntries);
        string hashHex = contentHash.ToString();
        if (TryGetDraft(hashHex, pending, out AddressListArtifactDraft existing))
        {
            match = Match(role, existing, negated);
            return null;
        }

        int pendingNewLists = pending.Keys.Count(key => !_listsByContentHash.ContainsKey(key));
        if (_listsByContentHash.Count + pendingNewLists >= Limits.MaxLists)
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.AddressListLimitExceeded,
                $"Address-list count would exceed {Limits.MaxLists}.");
        }

        int pendingFamilyEntries = pending
            .Where(kv => !_listsByContentHash.ContainsKey(kv.Key) && kv.Value.Family == family)
            .Sum(static kv => kv.Value.Entries.Count);
        int familyCount = _entryCountByFamily.GetValueOrDefault(family) + pendingFamilyEntries;
        if (familyCount + sealedEntries.Length > Limits.MaxEntriesPerFamily)
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.AddressEntryLimitExceeded,
                $"Address-list entries for {RouterOsFilterArtifactIdentity.FormatFamily(family)} would exceed {Limits.MaxEntriesPerFamily}.");
        }

        string listId = hashHex[..RouterOsFilterArtifactIdentity.ArtifactIdHexLength];
        string name = ManagedChainNamespace.AddressListName(family, listId);
        if (NameTakenByDifferentContent(name, hashHex, pending))
        {
            match = Universe(role);
            return AddressListCompileResult.Fail(
                PolicyCompilerCodes.ResourceNameCollision,
                $"Address-list name '{name}' already maps to a different content hash.");
        }

        AddressListArtifactDraft draft = new()
        {
            Family = family,
            Name = name,
            Entries = sealedEntries,
        };
        pending[hashHex] = draft;
        match = Match(role, draft, negated);
        return null;
    }

    private bool TryGetDraft(
        string hashHex,
        Dictionary<string, AddressListArtifactDraft> pending,
        out AddressListArtifactDraft draft)
    {
        if (pending.TryGetValue(hashHex, out AddressListArtifactDraft? pendingDraft))
        {
            draft = pendingDraft;
            return true;
        }

        if (_listsByContentHash.TryGetValue(hashHex, out AddressListArtifactDraft? existing))
        {
            draft = existing;
            return true;
        }

        draft = null!;
        return false;
    }

    private bool NameTakenByDifferentContent(
        string name,
        string hashHex,
        Dictionary<string, AddressListArtifactDraft> pending)
    {
        if (_contentHashByName.TryGetValue(name, out string? committedHash)
            && !string.Equals(committedHash, hashHex, StringComparison.Ordinal))
        {
            return true;
        }

        foreach ((string pendingHash, AddressListArtifactDraft draft) in pending)
        {
            if (string.Equals(draft.Name, name, StringComparison.Ordinal)
                && !string.Equals(pendingHash, hashHex, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void Commit(Dictionary<string, AddressListArtifactDraft> pending)
    {
        foreach ((string hashHex, AddressListArtifactDraft draft) in pending)
        {
            if (_listsByContentHash.ContainsKey(hashHex))
            {
                continue;
            }

            _listsByContentHash[hashHex] = draft;
            _contentHashByName[draft.Name] = hashHex;
            _entryCountByFamily[draft.Family] = _entryCountByFamily.GetValueOrDefault(draft.Family) + draft.Entries.Count;
        }
    }

    private static CompiledAddressSelector Universe(AddressListMatcherRole role)
        => new()
        {
            Role = role,
            EmitsMatcher = false,
        };

    private static CompiledAddressSelector Match(
        AddressListMatcherRole role,
        AddressListArtifactDraft list,
        bool negated)
    {
        string key = role == AddressListMatcherRole.Source ? "src-address-list" : "dst-address-list";
        string value = negated ? "!" + list.Name : list.Name;
        return new CompiledAddressSelector
        {
            Role = role,
            EmitsMatcher = true,
            Negated = negated,
            MatcherKey = key,
            MatcherValue = value,
            List = list,
        };
    }

    private static string FormatRole(AddressListMatcherRole role)
        => role == AddressListMatcherRole.Source ? "Source" : "Destination";
}
