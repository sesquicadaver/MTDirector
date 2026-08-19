using System.Text.Json;
using Xunit;

namespace Mfc.RouterOs.IntegrationTests;

/// <summary>
/// Verifies the CHR testlab skeleton contracts exist without requiring a live CHR image.
/// </summary>
public sealed class ChrLabSkeletonTests
{
    private static readonly string[] RequiredTopologies =
    [
        "standalone",
        "multi-wan-failover",
        "multi-wan-balanced",
        "vrrp-active-passive",
        "vrrp-split-master",
        "standalone-dual-stack",
        "crs-switch",
    ];

    private static string RepoRoot
    {
        get
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

            throw new InvalidOperationException("Repository root not found from test base directory.");
        }
    }

    [Fact]
    public void ManifestExampleDefinesRequiredTopologiesAndIsolationFlags()
    {
        string path = Path.Combine(RepoRoot, "testlab", "chr", "manifest.example.json");
        Assert.True(File.Exists(path), "manifest.example.json must exist");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = doc.RootElement;

        Assert.Equal("x86_64", root.GetProperty("architecture").GetString());
        HashSet<string> required = root.GetProperty("requiredTopologies")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string topology in RequiredTopologies)
        {
            Assert.Contains(topology, required);
        }

        JsonElement isolation = root.GetProperty("isolation");
        Assert.True(isolation.GetProperty("forbidProductionRoutes").GetBoolean());
        Assert.True(isolation.GetProperty("requireEphemeralTestCa").GetBoolean());
        Assert.True(isolation.GetProperty("requireCredentialRotationPerRun").GetBoolean());
    }

    [Fact]
    public void EachTopologyContractDefinesResetCleanupAndEphemeralCredentials()
    {
        foreach (string topology in RequiredTopologies)
        {
            string path = Path.Combine(RepoRoot, "testlab", "chr", "topologies", topology, "topology.json");
            Assert.True(File.Exists(path), $"Missing topology contract: {topology}");

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            Assert.Equal(topology, root.GetProperty("id").GetString());
            Assert.True(root.GetProperty("credentials").GetProperty("reuseForbidden").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reset").GetProperty("snapshotName").GetString()));
            Assert.NotEmpty(root.GetProperty("reset").GetProperty("steps").EnumerateArray());
            Assert.NotEmpty(root.GetProperty("cleanup").GetProperty("steps").EnumerateArray());

            string fixtureRelative = root.GetProperty("fixture").GetString()!;
            string fixturePath = Path.Combine(RepoRoot, "testlab", "chr", fixtureRelative);
            Assert.True(File.Exists(fixturePath), $"Missing fixture for {topology}: {fixtureRelative}");
        }
    }

    [Fact]
    public void RepositoryDoesNotContainChrImagesOrLicenseFiles()
    {
        string chrRoot = Path.Combine(RepoRoot, "testlab", "chr");
        string[] forbiddenExtensions = [".img", ".qcow2", ".vmdk", ".lic", ".key", ".pem", ".pfx"];

        IEnumerable<string> hits = Directory.EnumerateFiles(chrRoot, "*", SearchOption.AllDirectories)
            .Where(path => forbiddenExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));

        Assert.Empty(hits);
    }

    [Fact]
    public void StandaloneProvisioningScriptExistsOutsideProductAdapter()
    {
        string script = Path.Combine(RepoRoot, "testlab", "chr", "scripts", "provision-standalone.sh");
        Assert.True(File.Exists(script), "M1-30 AC#11 requires testlab provisioning outside Mfc.RouterOs.");
        string text = File.ReadAllText(script);
        Assert.Contains("OUTSIDE Mfc.RouterOs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mfc.RouterOs", Path.GetDirectoryName(script)!, StringComparison.Ordinal);
    }

    [Fact]
    public void MultiWanTopologiesDeclareDistinctUplinkRolesAndProvisionScript()
    {
        string failover = Path.Combine(RepoRoot, "testlab", "chr", "topologies", "multi-wan-failover", "topology.json");
        string balanced = Path.Combine(RepoRoot, "testlab", "chr", "topologies", "multi-wan-balanced", "topology.json");
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(failover)))
        {
            JsonElement wans = doc.RootElement.GetProperty("wanSimulators");
            Assert.Equal(2, wans.GetArrayLength());
            Assert.Equal("primary", wans[0].GetProperty("role").GetString());
            Assert.Equal("secondary", wans[1].GetProperty("role").GetString());
        }

        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(balanced)))
        {
            JsonElement wans = doc.RootElement.GetProperty("wanSimulators");
            Assert.Equal(2, wans.GetArrayLength());
            Assert.Equal("balanced", wans[0].GetProperty("role").GetString());
            Assert.Equal("balanced", wans[1].GetProperty("role").GetString());
        }

        string script = Path.Combine(RepoRoot, "testlab", "chr", "scripts", "provision-multi-wan.sh");
        Assert.True(File.Exists(script), "M1-31 requires multi-WAN provisioning outside Mfc.RouterOs.");
        string text = File.ReadAllText(script);
        Assert.Contains("OUTSIDE Mfc.RouterOs", text, StringComparison.Ordinal);
        Assert.Contains("failover", text, StringComparison.Ordinal);
        Assert.Contains("balanced", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VrrpTopologiesDeclareDistinctMembersAndProvisionScript()
    {
        string active = Path.Combine(RepoRoot, "testlab", "chr", "topologies", "vrrp-active-passive", "topology.json");
        string split = Path.Combine(RepoRoot, "testlab", "chr", "topologies", "vrrp-split-master", "topology.json");
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(active)))
        {
            JsonElement addresses = doc.RootElement.GetProperty("management").GetProperty("deviceAddresses");
            Assert.Equal(2, addresses.GetArrayLength());
            Assert.Equal("10.255.40.10", addresses[0].GetString());
            Assert.Equal("10.255.40.11", addresses[1].GetString());
            Assert.Equal("10.255.40.20", doc.RootElement.GetProperty("management").GetProperty("virtualAddress").GetString());
        }

        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(split)))
        {
            JsonElement addresses = doc.RootElement.GetProperty("management").GetProperty("deviceAddresses");
            Assert.Equal(2, addresses.GetArrayLength());
            Assert.Equal("10.255.50.10", addresses[0].GetString());
            Assert.Equal("10.255.50.11", addresses[1].GetString());
            Assert.Equal(2, doc.RootElement.GetProperty("wanSimulators").GetArrayLength());
        }

        string script = Path.Combine(RepoRoot, "testlab", "chr", "scripts", "provision-vrrp.sh");
        Assert.True(File.Exists(script), "M1-32 requires VRRP provisioning outside Mfc.RouterOs.");
        string text = File.ReadAllText(script);
        Assert.Contains("OUTSIDE Mfc.RouterOs", text, StringComparison.Ordinal);
        Assert.Contains("active-passive", text, StringComparison.Ordinal);
        Assert.Contains("split-master", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingExtraTopologiesHaveProvisionScriptOutsideProductAdapter()
    {
        string script = Path.Combine(RepoRoot, "testlab", "chr", "scripts", "provision-onboarding-extra.sh");
        Assert.True(File.Exists(script), "M5-10 requires extra topology provisioning outside Mfc.RouterOs.");
        string text = File.ReadAllText(script);
        Assert.Contains("OUTSIDE Mfc.RouterOs", text, StringComparison.Ordinal);
        Assert.Contains("standalone-dual-stack", text, StringComparison.Ordinal);
        Assert.Contains("crs-switch", text, StringComparison.Ordinal);
    }
}
