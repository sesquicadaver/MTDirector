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
}
