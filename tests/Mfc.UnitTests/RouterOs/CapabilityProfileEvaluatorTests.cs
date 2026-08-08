using System.Text.Json;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Capabilities;
using Mfc.RouterOs.Discovery;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class CapabilityProfileEvaluatorTests
{
    [Fact]
    public void ManifestHasVersionedSchemaAndRequiredSections()
    {
        CompatibilityManifestDocument manifest = CompatibilityManifestLoader.Load();
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(manifest.ProfileId));
        Assert.Contains("7.16.2", manifest.SupportedRouterOsBuilds);
        Assert.Contains("x86_64", manifest.Architectures);
        Assert.Contains("chr", manifest.BoardClasses);
        Assert.NotEmpty(manifest.RequiredMenus);
        Assert.NotEmpty(manifest.RequiredProperties);
        Assert.NotEmpty(manifest.KnownIncompatibilities);
        Assert.NotEqual(default, CompatibilityManifestLoader.ManifestHash);
    }

    [Fact]
    public void SupportedBuildProducesSupportedStateAndDeterministicHashWithoutObservations()
    {
        CapabilityEvaluationResult first = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.16.2 (stable)",
            architecture: "x86_64",
            board: "CHR",
            packages: [("routeros", "false"), ("ipv6", "false")],
            uptime: "9d9h"));
        CapabilityEvaluationResult second = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.16.2-stable",
            architecture: "x86_64",
            board: "CHR",
            packages: [("ipv6", "false"), ("routeros", "false")],
            uptime: "1s"));

        Assert.Equal(SupportState.Supported, first.Profile.SupportState);
        Assert.Equal(BoardClass.Chr, first.BoardClass);
        Assert.True(first.Profile.VrrpSupported);
        Assert.True(first.Profile.BridgeSupported);
        Assert.True(first.Profile.Ipv6Supported);
        Assert.Equal(first.CapabilityHash, second.CapabilityHash);
        Assert.Equal(first.Profile.CompatibilityManifestHash, CompatibilityManifestLoader.ManifestHash);
        Assert.DoesNotContain("uptime", first.CapabilityHash.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownVersionNeedsRevalidation()
    {
        CapabilityEvaluationResult result = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.99.9",
            architecture: "x86_64",
            board: "CHR"));

        Assert.Equal(SupportState.NeedsRevalidation, result.Profile.SupportState);
        Assert.Contains("VERSION_UNKNOWN_NEEDS_REVALIDATION", result.Findings);
    }

    [Fact]
    public void RouterOs6IsReadOnlyNotWriteCapable()
    {
        CapabilityEvaluationResult result = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "6.49.17",
            architecture: "x86_64",
            board: "RB4011"));

        Assert.Equal(SupportState.ReadOnly, result.Profile.SupportState);
        Assert.Contains("ROS6_READ_ONLY", result.Findings);
        Assert.False(result.Profile.VrrpSupported);
    }

    [Theory]
    [InlineData("7.16.2-testing")]
    [InlineData("7.16.2-development")]
    public void TestingAndDevelopmentChannelsNeverGetWriteSupport(string version)
    {
        CapabilityEvaluationResult result = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: version,
            architecture: "x86_64",
            board: "CHR"));

        Assert.Equal(SupportState.ReadOnly, result.Profile.SupportState);
        Assert.Contains("NON_PRODUCTION_CHANNEL_READ_ONLY", result.Findings);
        Assert.NotEqual(SupportState.Supported, result.Profile.SupportState);
    }

    [Fact]
    public void CapabilityChangeInvalidatesTopologyValidationCache()
    {
        CapabilityEvaluationResult a = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.16.2",
            architecture: "x86_64",
            board: "CHR"));
        CapabilityEvaluationResult b = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.16.1",
            architecture: "x86_64",
            board: "CHR"));

        TopologyValidationCache cache = new();
        cache.RememberValidated(a.CapabilityHash);
        Assert.True(cache.IsValidFor(a.CapabilityHash));
        Assert.True(b.InvalidatesTopologyValidation(a.CapabilityHash));
        Assert.True(cache.InvalidateIfCapabilityChanged(b.CapabilityHash));
        Assert.False(cache.IsValidFor(a.CapabilityHash));
        Assert.False(cache.IsValidFor(b.CapabilityHash));
    }

    [Fact]
    public void ManifestFixtureDocumentsRequiredSections()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "compatibility-manifest.fixture.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, doc.RootElement.GetProperty("expectedSchemaVersion").GetInt32());
        Assert.Equal(CompatibilityManifestLoader.ExpectedSchemaVersion, doc.RootElement.GetProperty("expectedSchemaVersion").GetInt32());
        Assert.Contains(
            doc.RootElement.GetProperty("requiredSections").EnumerateArray().Select(e => e.GetString()),
            s => s == "knownIncompatibilities");
    }

    [Fact]
    public void BoardClassUsesModelNotVersionAlone()
    {
        CapabilityEvaluationResult crs = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.16.2",
            architecture: "arm64",
            board: "CRS326-24G-2S+",
            model: "CRS326-24G-2S+"));
        CapabilityEvaluationResult router = CapabilityProfileEvaluator.Evaluate(Discovery(
            version: "7.16.2",
            architecture: "arm64",
            board: "CCR2004-1G-12S+2XS",
            model: "CCR2004-1G-12S+2XS"));

        Assert.Equal(BoardClass.Crs, crs.BoardClass);
        Assert.Equal(BoardClass.Router, router.BoardClass);
        Assert.Equal(SupportState.Supported, crs.Profile.SupportState);
        Assert.Equal(SupportState.Supported, router.Profile.SupportState);
        Assert.NotEqual(crs.CapabilityHash, router.CapabilityHash);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static SystemServiceDiscoveryResult Discovery(
        string version,
        string architecture,
        string board,
        string? model = null,
        (string Name, string Disabled)[]? packages = null,
        string? uptime = "1h")
    {
        packages ??= [("routeros", "false"), ("ipv6", "false")];
        return new SystemServiceDiscoveryResult
        {
            Identity = new SystemIdentityDiscovery
            {
                Name = "lab-device",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Resource = new SystemResourceDiscovery
            {
                Version = version,
                BuildTime = "2024-01-01 00:00:00",
                ArchitectureName = architecture,
                BoardName = board,
                Platform = "MikroTik",
                Uptime = uptime,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["free-memory"] = "123",
                },
            },
            Routerboard = new SystemRouterboardDiscovery
            {
                Available = model is not null,
                Routerboard = model is null ? null : "true",
                Model = model,
                SerialNumber = null,
                FirmwareType = null,
                FactoryFirmware = null,
                CurrentFirmware = null,
                UpgradeFirmware = null,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Packages = packages.Select(p => new SystemPackageDiscovery
            {
                Id = null,
                Name = p.Name,
                Version = version.Split(' ', '-')[0],
                BuildTime = null,
                Scheduled = null,
                Disabled = p.Disabled,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            }).ToArray(),
            Clock = new SystemClockDiscovery
            {
                Time = "12:00:00",
                Date = "2026-08-08",
                TimeZoneName = "UTC",
                GmtOffset = "+00:00",
                DstActive = "false",
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            ApiSsl = new ApiSslServiceDiscovery
            {
                Found = true,
                Disabled = false,
                Port = "8729",
                AddressPrefixes = null,
                Certificate = "api-ssl",
                TlsVersion = "only-1.2",
                Vrf = null,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            Warnings = [],
        };
    }
}
