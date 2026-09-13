using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Discovery;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class RouterOsClockParserTests
{
    [Fact]
    public void ParsesIsoDateTimeWithGmtOffset()
    {
        DateTimeOffset parsed = RouterOsClockParser.Parse(Clock(
            date: "2026-09-13",
            time: "19:45:30",
            gmtOffset: "+03:00"));
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 19, 45, 30, TimeSpan.FromHours(3)), parsed);
    }

    [Fact]
    public void ParsesMikroTikMonthSlashDate()
    {
        DateTimeOffset parsed = RouterOsClockParser.Parse(Clock(
            date: "sep/13/2026",
            time: "19:45:30",
            gmtOffset: "00:00"));
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 19, 45, 30, TimeSpan.Zero), parsed);
    }

    [Fact]
    public void ParsesGmtPrefixedOffset()
    {
        DateTimeOffset parsed = RouterOsClockParser.Parse(Clock(
            date: "2026-09-13",
            time: "12:00:00",
            gmtOffset: "gmt+02:00"));
        Assert.Equal(TimeSpan.FromHours(2), parsed.Offset);
    }

    [Fact]
    public void RejectsIncompleteFields()
    {
        Assert.Throws<InvalidOperationException>(() => RouterOsClockParser.Parse(Clock(
            date: null,
            time: "19:45:30",
            gmtOffset: "00:00")));
        Assert.Throws<InvalidOperationException>(() => RouterOsClockParser.Parse(Clock(
            date: "2026-09-13",
            time: "bad",
            gmtOffset: "00:00")));
    }

    private static SystemClockDiscovery Clock(string? date, string? time, string? gmtOffset)
        => new()
        {
            Date = date,
            Time = time,
            TimeZoneName = "UTC",
            GmtOffset = gmtOffset,
            DstActive = "false",
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
}
