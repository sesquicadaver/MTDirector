using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Diff;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Topology;

/// <summary>Severity for VRRP pair consistency findings (Node-scoped, read-only).</summary>
public enum VrrpPairFindingSeverity : byte
{
    Finding = 0,
    Blocker = 1,
}

/// <summary>One Node-scoped VRRP pair consistency finding.</summary>
public sealed record VrrpPairConsistencyFinding
{
    public const string NotVrrpNode = "VRRP_PAIR_NOT_VRRP_NODE";
    public const string InsufficientMembers = "VRRP_PAIR_INSUFFICIENT_MEMBERS";
    public const string MissingCapture = "VRRP_PAIR_MISSING_CAPTURE";
    public const string GroupMembershipMismatch = "VRRP_PAIR_GROUP_MEMBERSHIP_MISMATCH";
    public const string ConfigFieldMismatch = "VRRP_PAIR_CONFIG_FIELD_MISMATCH";
    public const string EqualPriorities = "VRRP_PAIR_EQUAL_PRIORITIES";
    public const string SplitMaster = "VRRP_PAIR_SPLIT_MASTER";
    public const string FilterLogicalMismatch = "VRRP_PAIR_FILTER_LOGICAL_MISMATCH";
    public const string DesiredLogicalHashMismatch = "VRRP_PAIR_DESIRED_LOGICAL_HASH_MISMATCH";
    public const string NoVrrpGroups = "VRRP_PAIR_NO_VRRP_GROUPS";

    public required string Code { get; init; }

    public required string Message { get; init; }

    public required VrrpPairFindingSeverity Severity { get; init; }

    public string? Subject { get; init; }

    public Guid? DeviceId { get; init; }
}

/// <summary>Per-device input for pair consistency (last capture sections + optional desired logical hash).</summary>
public sealed record VrrpPairMemberInput
{
    public required DeviceId DeviceId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Canonical sections from the member's last completed capture; empty when missing.</summary>
    public required IReadOnlyList<CanonicalSection> Sections { get; init; }

    /// <summary>Optional desired/logical effective policy hash hex (32-byte SHA-256 hex); null when unset.</summary>
    public string? DesiredLogicalHashHex { get; init; }
}

/// <summary>Result of Node-scoped VRRP pair consistency analysis.</summary>
public sealed class VrrpPairConsistencyResult
{
    public required NodeId NodeId { get; init; }

    public required bool Passed { get; init; }

    public required IReadOnlyList<VrrpPairConsistencyFinding> Findings { get; init; }

    public required int MemberCount { get; init; }

    public required int CaptureCount { get; init; }
}

/// <summary>
/// Read-only cross-member agreement for a VRRP Node from last-capture canonical sections.
/// Does not invent Master/Backup; does not compare snapshots via SemanticDiffEngine a↔b.
/// </summary>
public static class VrrpPairConsistencyAnalyzer
{
    /// <summary>
    /// Admin-critical <c>ha.vrrp</c> configuration fields that MUST match across members of the same family+VRID.
    /// Excludes <c>priority</c> (must differ), <c>name</c>/<c>interface</c>/<c>group</c> (physical).
    /// </summary>
    public static readonly string[] AgreementConfigFields =
    [
        "addresses",
        "version",
        "interval",
        "preemption-mode",
        "disabled",
        "sync-connection-tracking",
        "connection-tracking-port",
        "remote-address",
    ];

    private static readonly string[] FirewallSectionIds =
    [
        CanonicalSectionIds.FirewallIpv4Filter,
        CanonicalSectionIds.FirewallIpv6Filter,
    ];

    public static VrrpPairConsistencyResult Analyze(
        Node node,
        IReadOnlyList<VrrpPairMemberInput> members)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(members);

        List<VrrpPairConsistencyFinding> findings = [];
        if (node.DeclaredKind != NodeKind.Vrrp)
        {
            findings.Add(new VrrpPairConsistencyFinding
            {
                Code = VrrpPairConsistencyFinding.NotVrrpNode,
                Message = $"Node '{node.Id}' is {node.DeclaredKind}; pair consistency applies only to VRRP.",
                Severity = VrrpPairFindingSeverity.Blocker,
                Subject = node.Id.ToString(),
            });
            return Finish(node.Id, members, findings);
        }

        if (members.Count < 2)
        {
            findings.Add(new VrrpPairConsistencyFinding
            {
                Code = VrrpPairConsistencyFinding.InsufficientMembers,
                Message = $"VRRP pair consistency requires at least two members; got {members.Count}.",
                Severity = VrrpPairFindingSeverity.Blocker,
                Subject = node.Id.ToString(),
            });
            return Finish(node.Id, members, findings);
        }

        foreach (VrrpPairMemberInput member in members)
        {
            if (member.Sections.Count == 0)
            {
                findings.Add(new VrrpPairConsistencyFinding
                {
                    Code = VrrpPairConsistencyFinding.MissingCapture,
                    Message =
                        $"Member '{member.DisplayName}' has no completed capture sections for pair consistency.",
                    Severity = VrrpPairFindingSeverity.Blocker,
                    Subject = member.DisplayName,
                    DeviceId = member.DeviceId.Value,
                });
            }
        }

        if (findings.Exists(static f => f.Code == VrrpPairConsistencyFinding.MissingCapture))
        {
            return Finish(node.Id, members, findings);
        }

        AnalyzeVrrpConfiguration(members, findings);
        AnalyzeFilterLogical(members, findings);
        AnalyzeDesiredLogicalHashes(members, findings);

        return Finish(node.Id, members, findings);
    }

    private static void AnalyzeVrrpConfiguration(
        IReadOnlyList<VrrpPairMemberInput> members,
        List<VrrpPairConsistencyFinding> findings)
    {
        Dictionary<(string Family, string Vrid), List<(VrrpPairMemberInput Member, IReadOnlyDictionary<string, string> Props)>> byGroup =
            new();

        foreach (VrrpPairMemberInput member in members)
        {
            foreach (CanonicalSection section in member.Sections)
            {
                if (!string.Equals(section.SectionId, CanonicalSectionIds.HaVrrp, StringComparison.Ordinal)
                    || section.Domain != CanonicalDomain.Configuration)
                {
                    continue;
                }

                foreach (CanonicalRecord record in section.Records)
                {
                    string family = GetProp(record.Properties, "family") ?? string.Empty;
                    string vrid = GetProp(record.Properties, "vrid") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(vrid))
                    {
                        continue;
                    }

                    (string, string) key = (family, vrid);
                    if (!byGroup.TryGetValue(key, out List<(VrrpPairMemberInput, IReadOnlyDictionary<string, string>)>? list))
                    {
                        list = [];
                        byGroup[key] = list;
                    }

                    list.Add((member, record.Properties));
                }
            }
        }

        if (byGroup.Count == 0)
        {
            findings.Add(new VrrpPairConsistencyFinding
            {
                Code = VrrpPairConsistencyFinding.NoVrrpGroups,
                Message = "No ha.vrrp configuration groups found on any member capture.",
                Severity = VrrpPairFindingSeverity.Blocker,
            });
            return;
        }

        foreach (((string Family, string Vrid) key, List<(VrrpPairMemberInput Member, IReadOnlyDictionary<string, string> Props)> rows)
                 in byGroup.OrderBy(static e => e.Key.Family, StringComparer.Ordinal)
                     .ThenBy(static e => e.Key.Vrid, StringComparer.Ordinal))
        {
            string subject = $"{key.Family}/vrid={key.Vrid}";
            HashSet<Guid> present = rows.Select(static r => r.Member.DeviceId.Value).ToHashSet();
            foreach (VrrpPairMemberInput member in members)
            {
                if (!present.Contains(member.DeviceId.Value))
                {
                    findings.Add(new VrrpPairConsistencyFinding
                    {
                        Code = VrrpPairConsistencyFinding.GroupMembershipMismatch,
                        Message = $"VRRP group {subject} is missing on member '{member.DisplayName}'.",
                        Severity = VrrpPairFindingSeverity.Blocker,
                        Subject = subject,
                        DeviceId = member.DeviceId.Value,
                    });
                }
            }

            foreach (string field in AgreementConfigFields)
            {
                HashSet<string> values = new(StringComparer.Ordinal);
                foreach ((_, IReadOnlyDictionary<string, string> props) in rows)
                {
                    values.Add(GetProp(props, field) ?? string.Empty);
                }

                if (values.Count > 1)
                {
                    findings.Add(new VrrpPairConsistencyFinding
                    {
                        Code = VrrpPairConsistencyFinding.ConfigFieldMismatch,
                        Message =
                            $"VRRP group {subject} field '{field}' disagrees across members: "
                            + string.Join(" | ", values.Order(StringComparer.Ordinal).Select(static v =>
                                string.IsNullOrEmpty(v) ? "∅" : v)),
                        Severity = VrrpPairFindingSeverity.Blocker,
                        Subject = subject + "/" + field,
                    });
                }
            }

            HashSet<string> priorities = new(StringComparer.Ordinal);
            foreach ((_, IReadOnlyDictionary<string, string> props) in rows)
            {
                string? priority = GetProp(props, "priority");
                if (!string.IsNullOrWhiteSpace(priority))
                {
                    priorities.Add(priority);
                }
            }

            if (priorities.Count == 1 && rows.Count >= 2)
            {
                findings.Add(new VrrpPairConsistencyFinding
                {
                    Code = VrrpPairConsistencyFinding.EqualPriorities,
                    Message =
                        $"VRRP group {subject} has identical priority '{priorities.First()}' on every member; "
                        + "priorities should differ for predictable master election.",
                    Severity = VrrpPairFindingSeverity.Finding,
                    Subject = subject + "/priority",
                });
            }

            AnalyzeSplitMaster(members, key.Family, key.Vrid, subject, findings);
        }
    }

    private static void AnalyzeSplitMaster(
        IReadOnlyList<VrrpPairMemberInput> members,
        string family,
        string vrid,
        string subject,
        List<VrrpPairConsistencyFinding> findings)
    {
        int masters = 0;
        foreach (VrrpPairMemberInput member in members)
        {
            foreach (CanonicalSection section in member.Sections)
            {
                if (!string.Equals(section.SectionId, CanonicalSectionIds.HaVrrp, StringComparison.Ordinal)
                    || section.Domain != CanonicalDomain.Observations)
                {
                    continue;
                }

                foreach (CanonicalRecord record in section.Records)
                {
                    string? group = GetProp(record.Properties, "group");
                    string? role = GetProp(record.Properties, "role");
                    bool matchesGroup = !string.IsNullOrWhiteSpace(group)
                        && group.Contains(family, StringComparison.OrdinalIgnoreCase)
                        && group.Contains("vrid=" + vrid, StringComparison.OrdinalIgnoreCase);
                    // Observation records may only carry role; also match via config pairing by group key shape.
                    if (!matchesGroup && !string.IsNullOrWhiteSpace(group))
                    {
                        continue;
                    }

                    if (string.Equals(role, "Master", StringComparison.OrdinalIgnoreCase))
                    {
                        masters++;
                    }
                }
            }
        }

        // Prefer counting Masters only when observations are present for the group.
        // Fallback: count Master roles across all ha.vrrp observations when family/vrid appear in group string.
        if (masters > 1)
        {
            findings.Add(new VrrpPairConsistencyFinding
            {
                Code = VrrpPairConsistencyFinding.SplitMaster,
                Message = $"Split-master detected for VRRP group {subject}: {masters} members report Master.",
                Severity = VrrpPairFindingSeverity.Blocker,
                Subject = subject,
            });
        }
    }

    private static void AnalyzeFilterLogical(
        IReadOnlyList<VrrpPairMemberInput> members,
        List<VrrpPairConsistencyFinding> findings)
    {
        foreach (string sectionId in FirewallSectionIds)
        {
            Dictionary<string, string> digestByMember = new(StringComparer.Ordinal);
            foreach (VrrpPairMemberInput member in members)
            {
                digestByMember[member.DisplayName] = ComputeFilterDigest(member.Sections, sectionId);
            }

            HashSet<string> distinct = digestByMember.Values.ToHashSet(StringComparer.Ordinal);
            if (distinct.Count <= 1)
            {
                continue;
            }

            findings.Add(new VrrpPairConsistencyFinding
            {
                Code = VrrpPairConsistencyFinding.FilterLogicalMismatch,
                Message =
                    $"Logical firewall section '{sectionId}' differs across VRRP members "
                    + "(normalized configuration fingerprints; physical interface names are not in filter projection).",
                Severity = VrrpPairFindingSeverity.Blocker,
                Subject = sectionId,
            });
        }
    }

    private static void AnalyzeDesiredLogicalHashes(
        IReadOnlyList<VrrpPairMemberInput> members,
        List<VrrpPairConsistencyFinding> findings)
    {
        List<string> present = members
            .Select(static m => m.DesiredLogicalHashHex)
            .Where(static h => !string.IsNullOrWhiteSpace(h))
            .Select(static h => h!.Trim().ToLowerInvariant())
            .ToList();
        if (present.Count < 2)
        {
            return;
        }

        if (present.Distinct(StringComparer.Ordinal).Count() > 1)
        {
            findings.Add(new VrrpPairConsistencyFinding
            {
                Code = VrrpPairConsistencyFinding.DesiredLogicalHashMismatch,
                Message =
                    "Desired/logical effective policy hash differs across VRRP members that have a hash set.",
                Severity = VrrpPairFindingSeverity.Blocker,
                Subject = "desired_logical_hash",
            });
        }
    }

    private static string ComputeFilterDigest(IReadOnlyList<CanonicalSection> sections, string sectionId)
    {
        CanonicalSection? section = sections.FirstOrDefault(s =>
            string.Equals(s.SectionId, sectionId, StringComparison.Ordinal)
            && s.Domain == CanonicalDomain.Configuration);
        if (section is null || section.Records.Count == 0)
        {
            return "empty";
        }

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int i = 0; i < section.Records.Count; i++)
        {
            string fp = RecordFingerprint.ComputeHex(section.Records[i].Properties);
            byte[] utf8 = Encoding.UTF8.GetBytes(fp);
            hasher.AppendData(utf8);
            hasher.AppendData([0]);
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    private static string? GetProp(IReadOnlyDictionary<string, string> props, string name)
        => props.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static VrrpPairConsistencyResult Finish(
        NodeId nodeId,
        IReadOnlyList<VrrpPairMemberInput> members,
        List<VrrpPairConsistencyFinding> findings)
    {
        VrrpPairConsistencyFinding[] ordered = findings
            .OrderBy(static f => f.Severity == VrrpPairFindingSeverity.Blocker ? 0 : 1)
            .ThenBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Subject ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();
        bool blockers = ordered.Any(static f => f.Severity == VrrpPairFindingSeverity.Blocker);
        return new VrrpPairConsistencyResult
        {
            NodeId = nodeId,
            Passed = !blockers,
            Findings = ordered,
            MemberCount = members.Count,
            CaptureCount = members.Count(static m => m.Sections.Count > 0),
        };
    }
}
