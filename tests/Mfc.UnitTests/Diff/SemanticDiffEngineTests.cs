using System.Globalization;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Diff;
using Xunit;

namespace Mfc.UnitTests.Diff;

public sealed class SemanticDiffEngineTests
{
    private const string ManagedUuid = "550e8400-e29b-41d4-a716-446655440000";

    [Fact]
    public void ObservationVrrpRoleChangeIsStateChangedNotModified()
    {
        CanonicalSection baseObs = BuildSection(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            Props(("group", "vrrp1"), ("role", "backup")));
        CanonicalSection targetObs = BuildSection(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            Props(("group", "vrrp1"), ("role", "master")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseObs], [targetObs]);
        DiffEntry entry = Assert.Single(doc.Entries);
        Assert.Equal(DiffDomain.Observation, entry.Domain);
        Assert.Contains(DiffChange.StateChanged, entry.Changes);
        Assert.DoesNotContain(DiffChange.Modified, entry.Changes);
        Assert.Equal(MatchConfidence.NaturalKey, entry.Confidence);
    }

    [Fact]
    public void OrderedExactFingerprintMoveEmitsMoved()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "forward"), ("action", "accept"), ("comment", "a"), ("ordinal", "0")),
            Props(("chain", "forward"), ("action", "drop"), ("comment", "b"), ("ordinal", "1")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "forward"), ("action", "drop"), ("comment", "b"), ("ordinal", "0")),
            Props(("chain", "forward"), ("action", "accept"), ("comment", "a"), ("ordinal", "1")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.Equal(2, doc.Entries.Count);
        Assert.All(doc.Entries, e =>
        {
            Assert.Contains(DiffChange.Moved, e.Changes);
            Assert.Equal(MatchConfidence.ExactFingerprint, e.Confidence);
            Assert.DoesNotContain(DiffChange.Modified, e.Changes);
        });
    }

    [Fact]
    public void FwcMarkerActionChangeIsModified()
    {
        string comment = $"fwc:rule:{ManagedUuid}:r1 allow";
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "input"), ("action", "accept"), ("comment", comment), ("ordinal", "0")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "input"), ("action", "drop"), ("comment", comment), ("ordinal", "0")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        DiffEntry entry = Assert.Single(doc.Entries);
        Assert.Contains(DiffChange.Modified, entry.Changes);
        Assert.Equal(MatchConfidence.ControllerId, entry.Confidence);
        Assert.Equal(ManagedUuid, entry.RecordKey);
        Assert.Contains(entry.FieldChanges, f => f.FieldName == "action" && f.Before == "accept" && f.After == "drop");
    }

    [Fact]
    public void UnmanagedUniqueFingerprintMoveIsMovedExactFingerprint()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "forward"), ("action", "accept"), ("comment", "unmanaged"), ("ordinal", "0")),
            Props(("chain", "forward"), ("action", "drop"), ("comment", "other"), ("ordinal", "1")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "forward"), ("action", "drop"), ("comment", "other"), ("ordinal", "0")),
            Props(("chain", "forward"), ("action", "accept"), ("comment", "unmanaged"), ("ordinal", "1")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.Contains(
            doc.Entries,
            e => e.Changes.Contains(DiffChange.Moved) && e.Confidence == MatchConfidence.ExactFingerprint);
        Assert.DoesNotContain(doc.Entries, e => e.Changes.Contains(DiffChange.Modified));
    }

    [Fact]
    public void UnmanagedContentChangeIsRemovedPlusAddedWithoutModified()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "forward"), ("action", "accept"), ("comment", "unmanaged"), ("ordinal", "0")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "forward"), ("action", "drop"), ("comment", "unmanaged"), ("ordinal", "0")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.Equal(2, doc.Entries.Count);
        Assert.Contains(doc.Entries, e => e.Changes.Contains(DiffChange.Removed));
        Assert.Contains(doc.Entries, e => e.Changes.Contains(DiffChange.Added));
        Assert.DoesNotContain(doc.Entries, e => e.Changes.Contains(DiffChange.Modified));
        Assert.All(doc.Entries, e => Assert.Equal(MatchConfidence.Conservative, e.Confidence));
    }

    [Fact]
    public void OrderedMidListInsertProducesAddedNotChaos()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "f"), ("action", "accept"), ("comment", "a"), ("ordinal", "0")),
            Props(("chain", "f"), ("action", "accept"), ("comment", "b"), ("ordinal", "1")),
            Props(("chain", "f"), ("action", "accept"), ("comment", "c"), ("ordinal", "2")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "f"), ("action", "accept"), ("comment", "a"), ("ordinal", "0")),
            Props(("chain", "f"), ("action", "drop"), ("comment", "x"), ("ordinal", "1")),
            Props(("chain", "f"), ("action", "accept"), ("comment", "b"), ("ordinal", "2")),
            Props(("chain", "f"), ("action", "accept"), ("comment", "c"), ("ordinal", "3")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.Contains(doc.Entries, e => e.Changes.Contains(DiffChange.Added));
        Assert.DoesNotContain(
            doc.Entries,
            e => e.Changes.Contains(DiffChange.Removed) && e.Confidence == MatchConfidence.Conservative);
    }

    [Fact]
    public void AddressListOrderIrrelevantAndNewEntryIsAdded()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4AddressLists,
            ordered: false,
            Props(("list", "block"), ("address", "10.0.0.2")),
            Props(("list", "block"), ("address", "10.0.0.1")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4AddressLists,
            ordered: false,
            Props(("list", "block"), ("address", "10.0.0.1")),
            Props(("list", "block"), ("address", "10.0.0.2")),
            Props(("list", "block"), ("address", "10.0.0.3")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        DiffEntry entry = Assert.Single(doc.Entries);
        Assert.Contains(DiffChange.Added, entry.Changes);
        Assert.Equal("block|10.0.0.3", entry.RecordKey);
        Assert.DoesNotContain(doc.Entries, e => e.Changes.Contains(DiffChange.Moved));
    }

    [Fact]
    public void InterfaceListMembersCsvSetFieldDiff()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.NetworkInterfaceLists,
            ordered: false,
            Props(("list", "WAN"), ("members", "ether1,ether2")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.NetworkInterfaceLists,
            ordered: false,
            Props(("list", "WAN"), ("members", "ether2,ether5")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        DiffEntry entry = Assert.Single(doc.Entries);
        Assert.Contains(DiffChange.Modified, entry.Changes);
        DiffFieldChange members = Assert.Single(entry.FieldChanges, f => f.FieldName == "members");
        Assert.Contains("ether5", members.AddedValues);
        Assert.Contains("ether1", members.RemovedValues);
    }

    [Fact]
    public void VrrpObservationStateChanged()
    {
        CanonicalSection baseObs = BuildSection(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            Props(("group", "g1"), ("role", "master")));
        CanonicalSection targetObs = BuildSection(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            Props(("group", "g1"), ("role", "backup")));

        DiffDocument doc = SemanticDiffEngine.Compare([baseObs], [targetObs]);
        Assert.Contains(Assert.Single(doc.Entries).Changes, c => c == DiffChange.StateChanged);
    }

    [Fact]
    public void DiffIsDeterministicAcrossRuns()
    {
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "f"), ("action", "accept"), ("comment", "a"), ("ordinal", "0")),
            Props(("chain", "f"), ("action", "drop"), ("comment", "b"), ("ordinal", "1")));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            Props(("chain", "f"), ("action", "drop"), ("comment", "b"), ("ordinal", "0")),
            Props(("chain", "f"), ("action", "accept"), ("comment", "a"), ("ordinal", "1")),
            Props(("chain", "f"), ("action", "reject"), ("comment", "c"), ("ordinal", "2")));

        DiffDocument first = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        DiffDocument second = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.Equal(Serialize(first), Serialize(second));
    }

    [Fact]
    public void IdenticalSectionsProduceEmptyDocument()
    {
        CanonicalSection section = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.NetworkInterfaces,
            ordered: false,
            Props(("name", "ether1"), ("mtu", "1500")));

        DiffDocument doc = SemanticDiffEngine.Compare([section], [section]);
        Assert.True(doc.Identical);
        Assert.Empty(doc.Entries);
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void HugeOrderedSectionEmitsComplexityWarningWithoutThrow()
    {
        const int count = 20_001;
        Dictionary<string, string>[] baseRecords = new Dictionary<string, string>[count];
        Dictionary<string, string>[] targetRecords = new Dictionary<string, string>[count];
        for (int i = 0; i < count; i++)
        {
            string ordinal = i.ToString(CultureInfo.InvariantCulture);
            baseRecords[i] = Props(
                ("chain", "f"),
                ("action", "accept"),
                ("comment", "r" + ordinal),
                ("ordinal", ordinal));
            // Shift comments so sections differ and unmatched work remains large.
            string targetOrdinal = ((i + 1) % count).ToString(CultureInfo.InvariantCulture);
            targetRecords[i] = Props(
                ("chain", "f"),
                ("action", "accept"),
                ("comment", "r" + targetOrdinal),
                ("ordinal", ordinal));
        }

        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            baseRecords);
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            targetRecords);

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.Contains(doc.Warnings, w => w.Code == "DIFF_COMPLEXITY_LIMIT");
        Assert.False(doc.Identical);
    }

    [Fact]
    public void DuplicateUnmanagedIdenticalFingerprintsDoNotFalseMove()
    {
        Dictionary<string, string> twin = Props(("chain", "f"), ("action", "accept"), ("comment", "twin"));
        CanonicalSection baseSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            CloneWithOrdinal(twin, 0),
            CloneWithOrdinal(twin, 1));
        CanonicalSection targetSec = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.FirewallIpv4Filter,
            ordered: true,
            CloneWithOrdinal(twin, 1),
            CloneWithOrdinal(twin, 0));

        DiffDocument doc = SemanticDiffEngine.Compare([baseSec], [targetSec]);
        Assert.DoesNotContain(doc.Entries, e => e.Changes.Contains(DiffChange.Moved));
    }

    [Fact]
    public void FwcRuleMarkerRejectsMalformedComments()
    {
        Assert.False(FwcRuleMarker.TryParse("rule:not-a-marker", out _));
        Assert.False(FwcRuleMarker.TryParse("fwc:rule:not-a-uuid:r1", out _));
        Assert.True(FwcRuleMarker.TryParse($"fwc:rule:{ManagedUuid}:r1 rest", out FwcRuleMarker.ParsedMarker marker));
        Assert.Equal(Guid.Parse(ManagedUuid), marker.Uuid);
        Assert.Equal("r1", marker.RevisionToken);
    }

    [Fact]
    public void CanonicalSectionTryParseRoundTrip()
    {
        CanonicalSection original = BuildSection(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.SystemIdentity,
            ordered: false,
            Props(("name", "gw1")));
        Assert.True(CanonicalSection.TryParse(original.Utf8Bytes, out CanonicalSection? parsed));
        Assert.NotNull(parsed);
        Assert.Equal(original.Utf8Bytes, parsed!.Utf8Bytes);
    }

    private static CanonicalSection BuildSection(
        CanonicalDomain domain,
        string sectionId,
        bool ordered,
        params Dictionary<string, string>[] records)
    {
        CanonicalRecord[] canonicalRecords = records
            .Select(static r => new CanonicalRecord(new Dictionary<string, string>(r, StringComparer.Ordinal)))
            .ToArray();
        return new CanonicalSection(domain, sectionId, ordered, canonicalRecords);
    }

    private static Dictionary<string, string> Props(params (string Key, string Value)[] pairs)
    {
        Dictionary<string, string> dict = new(StringComparer.Ordinal);
        foreach ((string key, string value) in pairs)
        {
            dict[key] = value;
        }

        return dict;
    }

    private static Dictionary<string, string> CloneWithOrdinal(Dictionary<string, string> source, int ordinal)
    {
        Dictionary<string, string> clone = new(source, StringComparer.Ordinal)
        {
            ["ordinal"] = ordinal.ToString(CultureInfo.InvariantCulture),
        };
        return clone;
    }

    private static string Serialize(DiffDocument document)
    {
        IEnumerable<string> entryLines = document.Entries.Select(e =>
            string.Join(
                '|',
                e.SectionId,
                e.Domain,
                string.Join(',', e.Changes),
                e.Confidence,
                e.RecordKey,
                e.BeforeOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-",
                e.AfterOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-"));
        IEnumerable<string> warningLines = document.Warnings.Select(w => w.Code + ":" + w.Message);
        return string.Join('\n', entryLines.Concat(warningLines));
    }
}
