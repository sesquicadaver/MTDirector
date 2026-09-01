using Mfc.Application.Mapping;
using Mfc.Domain.Canonicalization;
using Xunit;

namespace Mfc.UnitTests.Application;

/// <summary>W6-01: last-capture system.resource version/board-name; VRRP labels stay observation-only.</summary>
public sealed class DeviceLastCaptureFactsTests
{
    [Fact]
    public void EmptySectionsStayEmpty()
    {
        DeviceLastCaptureFacts facts = DeviceLastCaptureFacts.FromCanonicalSections([]);
        Assert.Empty(facts.VrrpRoleLabels);
        Assert.Null(facts.RouterOsVersion);
        Assert.Null(facts.Model);
    }

    [Fact]
    public void ProjectsVersionAndBoardNameWithoutInventingReachability()
    {
        CanonicalSection resource = new(
            CanonicalDomain.Observations,
            CanonicalSectionIds.SystemResource,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["version"] = "7.16.2 (stable)",
                    ["uptime"] = "1d2h",
                    ["board-name"] = "CHR",
                }),
            ]);

        DeviceLastCaptureFacts facts = DeviceLastCaptureFacts.FromCanonicalSections([resource]);
        Assert.Equal("7.16.2 (stable)", facts.RouterOsVersion);
        Assert.Equal("CHR", facts.Model);
        Assert.Empty(facts.VrrpRoleLabels);
    }

    [Fact]
    public void ConfigurationResourceDoesNotFillVersion()
    {
        CanonicalSection config = new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.SystemResource,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["version"] = "should-not-use",
                    ["board-name"] = "should-not-use",
                }),
            ]);

        DeviceLastCaptureFacts facts = DeviceLastCaptureFacts.FromCanonicalSections([config]);
        Assert.Null(facts.RouterOsVersion);
        Assert.Null(facts.Model);
    }
}
