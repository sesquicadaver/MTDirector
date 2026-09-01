using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W6-01: fingerprint RecordKey must not lead operator-facing snapshot/diff lines.</summary>
public sealed class SnapshotPresentationIdentityTests
{
    [Fact]
    public void FingerprintKeyIsDetected()
    {
        string hex = new('a', 64);
        Assert.True(SnapshotPresentationIdentity.IsFingerprintKey(hex));
        Assert.False(SnapshotPresentationIdentity.IsFingerprintKey("fwc:rule:1"));
        Assert.False(SnapshotPresentationIdentity.IsFingerprintKey(new string('a', 63)));
    }

    [Fact]
    public void RecordSummaryOmitsFingerprintAndKeepsFields()
    {
        string hex = new('b', 64);
        SnapshotFieldLine[] fields =
        [
            new() { Name = "chain", Value = "forward" },
            new() { Name = "action", Value = "drop" },
            new() { Name = "comment", Value = "lab" },
            new() { Name = "protocol", Value = "tcp" },
            new() { Name = "src-address", Value = "10.0.0.0/8" },
        ];

        string line = SnapshotPresentationIdentity.FormatRecordSummary(hex, "3", fields, hasMoreFields: true);
        Assert.DoesNotContain(hex, line, StringComparison.Ordinal);
        Assert.StartsWith("#3 · ", line, StringComparison.Ordinal);
        Assert.Contains("chain=forward", line, StringComparison.Ordinal);
        Assert.EndsWith(" …", line, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffIdentityUsesFieldLinesInsteadOfFingerprint()
    {
        string hex = new('c', 64);
        SnapshotDiffFieldLine[] fields =
        [
            new() { FieldName = "chain", Summary = "chain=input" },
            new() { FieldName = "action", Summary = "action=accept" },
        ];

        string identity = SnapshotPresentationIdentity.FormatDiffIdentity(hex, "order: 0 → 1", fields);
        Assert.DoesNotContain(hex, identity, StringComparison.Ordinal);
        Assert.Contains("chain=input", identity, StringComparison.Ordinal);
        Assert.Equal("fwc:rule:1", SnapshotPresentationIdentity.FormatDiffIdentity("fwc:rule:1", "order: —", []));
    }

    [Fact]
    public void PreferOperatorFacingPicksIpv4FilterFirst()
    {
        SnapshotSectionListItem[] sections =
        [
            Section("bridge.instances"),
            Section("firewall.ipv4.filter"),
            Section("ha.vrrp"),
        ];

        SnapshotSectionListItem? preferred = SnapshotPresentationIdentity.PreferOperatorFacingSection(sections);
        Assert.NotNull(preferred);
        Assert.Equal("firewall.ipv4.filter", preferred.SectionId);

        IReadOnlyList<SnapshotSectionListItem> ordered = SnapshotPresentationIdentity.OrderOperatorFacing(sections);
        Assert.Equal("firewall.ipv4.filter", ordered[0].SectionId);
        Assert.Equal("ha.vrrp", ordered[1].SectionId);
        Assert.Equal("bridge.instances", ordered[2].SectionId);
    }

    private static SnapshotSectionListItem Section(string id)
        => new()
        {
            SectionId = id,
            StatusText = "Ok",
            Ordered = id.StartsWith("firewall.", StringComparison.Ordinal),
            ConfigurationRecordCount = 1,
            ObservationRecordCount = 0,
            IsTechnicalOnly = false,
        };
}
