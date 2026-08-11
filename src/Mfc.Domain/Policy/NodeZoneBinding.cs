using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Desired Node→zone interface binding (Policy Model §21).
/// Distinct from inventory observation <c>ZoneBinding</c>.
/// </summary>
public sealed class NodeZoneBinding
{
    public const string DependencyHashPrefix = "mfc.zone.dependency.v1";

    private readonly List<string> _values;

    public NodeZoneBindingId Id { get; }

    public NodeId NodeId { get; }

    public ZoneId ZoneId { get; }

    public NodeZoneBindingKind Kind { get; }

    public IReadOnlyList<string> Values => _values;

    public Hash256 ExpectedDependencyHash { get; private set; }

    public Hash256? LastResolvedDependencyHash { get; private set; }

    public bool AnalysisStale { get; private set; }

    public ulong RowVersion { get; private set; }

    private NodeZoneBinding(
        NodeZoneBindingId id,
        NodeId nodeId,
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        List<string> values,
        Hash256 expectedDependencyHash,
        Hash256? lastResolvedDependencyHash,
        bool analysisStale,
        ulong rowVersion)
    {
        Id = id;
        NodeId = nodeId;
        ZoneId = zoneId;
        Kind = kind;
        _values = values;
        ExpectedDependencyHash = expectedDependencyHash;
        LastResolvedDependencyHash = lastResolvedDependencyHash;
        AnalysisStale = analysisStale;
        RowVersion = rowVersion;
    }

    public static NodeZoneBinding Create(
        NodeId nodeId,
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IEnumerable<string> values,
        Hash256 expectedDependencyHash)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(expectedDependencyHash);
        List<string> normalized = NormalizeValues(kind, values);
        return new NodeZoneBinding(
            NodeZoneBindingId.New(),
            nodeId,
            zoneId,
            kind,
            normalized,
            expectedDependencyHash,
            lastResolvedDependencyHash: null,
            analysisStale: true,
            rowVersion: 1);
    }

    public static NodeZoneBinding Reconstitute(
        NodeZoneBindingId id,
        NodeId nodeId,
        ZoneId zoneId,
        NodeZoneBindingKind kind,
        IEnumerable<string> values,
        Hash256 expectedDependencyHash,
        Hash256? lastResolvedDependencyHash,
        bool analysisStale,
        ulong rowVersion)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(expectedDependencyHash);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        return new NodeZoneBinding(
            id,
            nodeId,
            zoneId,
            kind,
            NormalizeValues(kind, values),
            expectedDependencyHash,
            lastResolvedDependencyHash,
            analysisStale,
            rowVersion);
    }

    public void ReplaceBinding(
        NodeZoneBindingKind kind,
        IEnumerable<string> values,
        Hash256 expectedDependencyHash)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(expectedDependencyHash);
        List<string> normalized = NormalizeValues(kind, values);
        // Kind is immutable on aggregate identity (node,zone); reject kind change.
        if (kind != Kind)
        {
            throw new DomainInvariantException("Node zone binding kind cannot change; delete and recreate.");
        }

        _values.Clear();
        _values.AddRange(normalized);
        ExpectedDependencyHash = expectedDependencyHash;
        AnalysisStale = true;
        Touch();
    }

    /// <summary>
    /// Records a fresh resolve outcome. Sets <see cref="AnalysisStale"/> when the fresh hash
    /// differs from <see cref="ExpectedDependencyHash"/> (AC#9 — mandatory signal).
    /// </summary>
    public void RecordResolve(Hash256 freshDependencyHash)
    {
        ArgumentNullException.ThrowIfNull(freshDependencyHash);
        ApplyResolveOutcome(freshDependencyHash, analysisStale: !ExpectedDependencyHash.Equals(freshDependencyHash));
    }

    /// <summary>
    /// Applies a multi-device (or single) resolve summary without last-writer-wins on stale.
    /// </summary>
    public void ApplyResolveOutcome(Hash256 freshDependencyHash, bool analysisStale)
    {
        ArgumentNullException.ThrowIfNull(freshDependencyHash);
        LastResolvedDependencyHash = freshDependencyHash;
        AnalysisStale = analysisStale;
        Touch();
    }

    public static Hash256 ComputeDependencyHash(
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        IReadOnlyList<string> resolvedMembers)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(resolvedMembers);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, DependencyHashPrefix);
        AppendNull(hasher);
        AppendUtf8(hasher, FormatKind(kind));
        AppendNull(hasher);
        foreach (string value in values.OrderBy(v => v, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, value);
            AppendNull(hasher);
        }

        AppendUtf8(hasher, "resolved");
        AppendNull(hasher);
        foreach (string member in resolvedMembers.OrderBy(v => v, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, member);
            AppendNull(hasher);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    public static string FormatKind(NodeZoneBindingKind kind)
        => kind switch
        {
            NodeZoneBindingKind.InterfaceList => "INTERFACE_LIST",
            NodeZoneBindingKind.SingleInterface => "SINGLE_INTERFACE",
            NodeZoneBindingKind.ExplicitInterfaceSet => "EXPLICIT_INTERFACE_SET",
            _ => throw new DomainInvariantException($"Unknown binding kind '{kind}'."),
        };

    private void Touch() => RowVersion++;

    private static List<string> NormalizeValues(NodeZoneBindingKind kind, IEnumerable<string> values)
    {
        List<string> result = [];
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DomainInvariantException("binding values must be non-empty.");
            }

            result.Add(value.Trim());
        }

        if (result.Count == 0)
        {
            throw new DomainInvariantException("Node zone binding requires at least one value.");
        }

        switch (kind)
        {
            case NodeZoneBindingKind.SingleInterface:
                if (result.Count != 1)
                {
                    throw new DomainInvariantException("SINGLE_INTERFACE requires exactly one value.");
                }

                break;

            case NodeZoneBindingKind.InterfaceList:
                if (result.Count != 1)
                {
                    throw new DomainInvariantException("INTERFACE_LIST requires exactly one list name.");
                }

                break;

            case NodeZoneBindingKind.ExplicitInterfaceSet:
                break;

            default:
                throw new DomainInvariantException($"Unknown binding kind '{kind}'.");
        }

        return result
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendNull(IncrementalHash hasher)
        => hasher.AppendData([(byte)0]);
}
