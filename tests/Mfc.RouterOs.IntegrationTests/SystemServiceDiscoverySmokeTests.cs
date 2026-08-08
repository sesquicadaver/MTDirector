using System.Text.Json;
using Xunit;

namespace Mfc.RouterOs.IntegrationTests;

/// <summary>
/// CHR-oriented smoke for M1-11 system/service discovery.
/// Uses the sanitized fixture when a live CHR image is not present (lab skeleton mode).
/// </summary>
public sealed class SystemServiceDiscoverySmokeTests
{
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
    public void SanitizedSystemServiceFixtureIsReadyForChrSmoke()
    {
        string path = Path.Combine(
            RepoRoot,
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "system-service-discovery.sanitized.json");
        Assert.True(File.Exists(path), "M1-11 sanitized discovery fixture must exist for CHR smoke.");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = doc.RootElement;

        Assert.Equal("lab-gw1", root.GetProperty("identity").GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("resource").GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("resource").GetProperty("architectureName").GetString()));
        Assert.True(root.GetProperty("apiSsl").GetProperty("found").GetBoolean());
        Assert.Equal("8729", root.GetProperty("apiSsl").GetProperty("port").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("apiSsl").GetProperty("certificate").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("apiSsl").GetProperty("addressPrefixes").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("clock").GetProperty("timeZoneName").GetString()));
        Assert.NotEmpty(root.GetProperty("packages").EnumerateArray());

        string payload = string.Concat(
            root.GetProperty("identity").ToString(),
            root.GetProperty("resource").ToString(),
            root.GetProperty("routerboard").ToString(),
            root.GetProperty("packages").ToString(),
            root.GetProperty("clock").ToString(),
            root.GetProperty("apiSsl").ToString());
        foreach (JsonElement forbidden in root.GetProperty("forbiddenAbsent").EnumerateArray())
        {
            string token = forbidden.GetString()!;
            Assert.DoesNotContain(token, payload, StringComparison.OrdinalIgnoreCase);
        }
    }
}
