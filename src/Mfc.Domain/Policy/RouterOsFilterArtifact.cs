using System.Collections.Immutable;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Immutable canonical RouterOS filter artifact (Compiler Spec §6–§7 / M3-01).
/// Payload contains address lists, chains, and desired anchor targets only — never API commands or RouterOS <c>.id</c>.
/// </summary>
public sealed class RouterOsFilterArtifact
{
    public const string DefaultLayoutVersion = "1";

    public uint SchemaVersion { get; }

    public string LayoutVersion { get; }

    public string ArtifactId { get; }

    public Hash256 PhysicalSemanticsHash { get; }

    public Hash256 CompilerProfileHash { get; }

    public DeviceId DeviceId { get; }

    public ImmutableArray<AddressListArtifact> AddressLists { get; }

    public ImmutableArray<ChainArtifact> Chains { get; }

    public ImmutableArray<AnchorTargetArtifact> AnchorTargets { get; }

    public Hash256 ResourceHash { get; }

    public ImmutableArray<byte> CanonicalBytes { get; }

    private RouterOsFilterArtifact(
        uint schemaVersion,
        string layoutVersion,
        string artifactId,
        Hash256 physicalSemanticsHash,
        Hash256 compilerProfileHash,
        DeviceId deviceId,
        ImmutableArray<AddressListArtifact> addressLists,
        ImmutableArray<ChainArtifact> chains,
        ImmutableArray<AnchorTargetArtifact> anchorTargets,
        Hash256 resourceHash,
        ImmutableArray<byte> canonicalBytes)
    {
        SchemaVersion = schemaVersion;
        LayoutVersion = layoutVersion;
        ArtifactId = artifactId;
        PhysicalSemanticsHash = physicalSemanticsHash;
        CompilerProfileHash = compilerProfileHash;
        DeviceId = deviceId;
        AddressLists = addressLists;
        Chains = chains;
        AnchorTargets = anchorTargets;
        ResourceHash = resourceHash;
        CanonicalBytes = canonicalBytes;
    }

    /// <summary>
    /// Builds a frozen artifact: sorts resources, derives <c>artifact_id</c>, writes MFC-CJ1 bytes, and seals <c>resource_hash</c>.
    /// </summary>
    public static RouterOsFilterArtifact Create(
        Hash256 compilerProfileHash,
        Hash256 physicalSemanticsHash,
        DeviceId deviceId,
        IReadOnlyList<AddressListArtifactDraft> addressLists,
        IReadOnlyList<ChainArtifactDraft> chains,
        IReadOnlyList<AnchorTargetArtifact> anchorTargets,
        string layoutVersion = DefaultLayoutVersion)
    {
        ArgumentNullException.ThrowIfNull(compilerProfileHash);
        ArgumentNullException.ThrowIfNull(physicalSemanticsHash);
        ArgumentNullException.ThrowIfNull(addressLists);
        ArgumentNullException.ThrowIfNull(chains);
        ArgumentNullException.ThrowIfNull(anchorTargets);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutVersion);

        string normalizedLayout = layoutVersion.Trim();
        string artifactId = RouterOsFilterArtifactIdentity.ComputeArtifactId(
            compilerProfileHash,
            physicalSemanticsHash,
            deviceId);

        List<AddressListArtifact> frozenLists = [];
        foreach (AddressListArtifactDraft draft in addressLists)
        {
            ArgumentNullException.ThrowIfNull(draft);
            frozenLists.Add(FreezeAddressList(draft));
        }

        ImmutableArray<AddressListArtifact> sealedLists = frozenLists
            .OrderBy(static l => RouterOsFilterArtifactIdentity.FormatFamily(l.Family), StringComparer.Ordinal)
            .ThenBy(static l => l.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        List<ChainArtifact> frozenChains = [];
        foreach (ChainArtifactDraft draft in chains)
        {
            ArgumentNullException.ThrowIfNull(draft);
            frozenChains.Add(FreezeChain(draft));
        }

        ImmutableArray<ChainArtifact> sealedChains = frozenChains
            .OrderBy(static c => RouterOsFilterArtifactIdentity.FormatFamily(c.Family), StringComparer.Ordinal)
            .ThenBy(static c => RouterOsFilterArtifactIdentity.FormatBuiltIn(c.BuiltInContext), StringComparer.Ordinal)
            .ThenBy(static c => RouterOsFilterArtifactIdentity.FormatRole(c.Role), StringComparer.Ordinal)
            .ThenBy(static c => c.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        ImmutableArray<AnchorTargetArtifact> sealedAnchors = anchorTargets
            .OrderBy(static a => RouterOsFilterArtifactIdentity.FormatFamily(a.Family), StringComparer.Ordinal)
            .ThenBy(static a => RouterOsFilterArtifactIdentity.FormatBuiltIn(a.BuiltInChain), StringComparer.Ordinal)
            .ThenBy(static a => a.ExpectedAnchorComment, StringComparer.Ordinal)
            .ThenBy(static a => a.DesiredJumpTarget, StringComparer.Ordinal)
            .ToImmutableArray();

        RouterOsFilterArtifact provisional = new(
            schemaVersion: 1,
            normalizedLayout,
            artifactId,
            physicalSemanticsHash,
            compilerProfileHash,
            deviceId,
            sealedLists,
            sealedChains,
            sealedAnchors,
            resourceHash: Hash256.Create(new byte[32]),
            canonicalBytes: ImmutableArray<byte>.Empty);

        byte[] canonical = RouterOsFilterArtifactCanonicalWriter.Write(provisional);
        Hash256 resourceHash = RouterOsFilterArtifactIdentity.HashResourceDocument(canonical);
        return new RouterOsFilterArtifact(
            schemaVersion: 1,
            normalizedLayout,
            artifactId,
            physicalSemanticsHash,
            compilerProfileHash,
            deviceId,
            sealedLists,
            sealedChains,
            sealedAnchors,
            resourceHash,
            canonical.ToImmutableArray());
    }

    private static AddressListArtifact FreezeAddressList(AddressListArtifactDraft draft)
    {
        if (draft.Family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported address-list family '{draft.Family}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        RouterOsFilterArtifactIdentity.EnsureAsciiLowerResourceName(draft.Name.Trim(), "address_list.name");
        ArgumentNullException.ThrowIfNull(draft.Entries);
        ImmutableArray<AddressListEntryArtifact> entries = draft.Entries
            .Select(static e => e ?? throw new DomainInvariantException("Address-list entry must not be null."))
            .OrderBy(static e => e.Address, StringComparer.Ordinal)
            .ToImmutableArray();
        Hash256 contentHash = RouterOsFilterArtifactIdentity.HashAddressListContent(draft.Family, entries);
        return new AddressListArtifact(draft.Family, draft.Name.Trim(), contentHash, entries);
    }

    private static ChainArtifact FreezeChain(ChainArtifactDraft draft)
    {
        if (draft.Family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported chain family '{draft.Family}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        RouterOsFilterArtifactIdentity.EnsureAsciiLowerResourceName(draft.Name.Trim(), "chain.name");
        ArgumentNullException.ThrowIfNull(draft.Rules);
        ImmutableArray<FilterRuleArtifact> rules = draft.Rules
            .Select(static r => r ?? throw new DomainInvariantException("Chain rule must not be null."))
            .OrderBy(static r => r.Ordinal)
            .ToImmutableArray();
        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i].Ordinal != (uint)i)
            {
                throw new DomainInvariantException(
                    "Filter chain rules must use contiguous physical ordinals starting at 0.");
            }
        }

        return new ChainArtifact(
            draft.Family,
            draft.BuiltInContext,
            draft.Name.Trim(),
            draft.Role,
            rules);
    }
}

/// <summary>Mutable draft used only while assembling an <see cref="RouterOsFilterArtifact"/>.</summary>
public sealed class AddressListArtifactDraft
{
    public required IpAddressFamily Family { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<AddressListEntryArtifact> Entries { get; init; }
}

/// <summary>Mutable draft used only while assembling an <see cref="RouterOsFilterArtifact"/>.</summary>
public sealed class ChainArtifactDraft
{
    public required IpAddressFamily Family { get; init; }

    public required FilterBuiltInContext BuiltInContext { get; init; }

    public required string Name { get; init; }

    public required FilterChainArtifactRole Role { get; init; }

    public required IReadOnlyList<FilterRuleArtifact> Rules { get; init; }
}
