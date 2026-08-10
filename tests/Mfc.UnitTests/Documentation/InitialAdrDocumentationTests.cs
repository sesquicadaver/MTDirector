using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// Guards M0-10 ADR deliverables: five Accepted ADRs with required sections.
/// </summary>
public sealed class InitialAdrDocumentationTests
{
    private static readonly string[] RequiredAdrFiles =
    [
        "0001-modular-monolith.md",
        "0002-routeros-api-ssl.md",
        "0003-node-deployment-atomicity.md",
        "0004-postgresql-source-of-truth.md",
        "0005-no-direct-desktop-routeros-access.md",
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
    public void RequiredAdrsExistWithAcceptedStatusAndSections()
    {
        string adrDir = Path.Combine(RepoRoot, "docs", "architecture", "adr");
        Assert.True(Directory.Exists(adrDir));

        foreach (string fileName in RequiredAdrFiles)
        {
            string path = Path.Combine(adrDir, fileName);
            Assert.True(File.Exists(path), $"Missing ADR file: {fileName}");
            string text = File.ReadAllText(path);
            Assert.Contains("**Status:** Accepted", text, StringComparison.Ordinal);
            Assert.Contains("## Context", text, StringComparison.Ordinal);
            Assert.Contains("## Decision", text, StringComparison.Ordinal);
            Assert.Contains("## Consequences", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DevelopmentDocsForLocalBuildMigrateAndChrExist()
    {
        string[] relativePaths =
        [
            "docs/architecture/overview.md",
            "docs/development/local-environment.md",
            "docs/development/testing.md",
            "docs/development/database-migrations.md",
            "docs/development/chr-lab.md",
            "docs/development/m1-vertical-slice-acceptance.md",
            "docs/development/connection-profiles.md",
            "docs/development/snapshots-and-diff.md",
            "docs/development/support-manifest.md",
            "docs/development/troubleshooting-read-path.md",
            "docs/operations/controller-configuration.md",
            "docs/operations/database-migrations.md",
            "docs/operations/recovery.md",
        ];

        foreach (string relative in relativePaths)
        {
            Assert.True(
                File.Exists(Path.Combine(RepoRoot, relative)),
                $"Missing documentation file: {relative}");
        }
    }
}
