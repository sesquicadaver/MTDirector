using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

public sealed class DesktopDisplayLabelsTests
{
    [Fact]
    public void FormatSectionTitleMapsFirewallFilterToWinboxPath()
    {
        Assert.Equal(
            "IP → Firewall → Filter Rules",
            DesktopDisplayLabels.FormatSectionTitle("firewall.ipv4.filter"));
    }

    [Fact]
    public void FormatPropertyNameMapsRouterOsKebabCaseToWinboxLabels()
    {
        Assert.Equal("Src. Address", DesktopDisplayLabels.FormatPropertyName("src-address"));
        Assert.Equal("Chain", DesktopDisplayLabels.FormatPropertyName("chain"));
    }

    [Fact]
    public void FormatPropertyLineUsesFriendlyLabel()
    {
        Assert.Equal("Action: drop", DesktopDisplayLabels.FormatPropertyLine("action", "drop"));
    }

    [Fact]
    public void FormatRecordSummaryFriendlyOmitsFingerprint()
    {
        string hex = new('a', 64);
        SnapshotFieldLine[] fields =
        [
            new() { Name = "chain", Value = "forward" },
            new() { Name = "action", Value = "drop" },
        ];

        string line = DesktopDisplayLabels.FormatRecordSummaryFriendly(hex, "2", fields, hasMoreFields: false);
        Assert.Contains("Chain: forward", line, StringComparison.Ordinal);
        Assert.Contains("Action: drop", line, StringComparison.Ordinal);
        Assert.DoesNotContain("chain=forward", line, StringComparison.Ordinal);
    }
}
