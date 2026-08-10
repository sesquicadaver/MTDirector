using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>M1-34: acceptance package documentation and Living Spec anchors must stay present.</summary>
public sealed class M1VerticalSliceAcceptanceDocumentationTests
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
    public void M1AcceptancePackageDocumentsExist()
    {
        string[] relativePaths =
        [
            "docs/development/m1-vertical-slice-acceptance.md",
            "docs/development/connection-profiles.md",
            "docs/development/snapshots-and-diff.md",
            "docs/development/support-manifest.md",
            "docs/development/troubleshooting-read-path.md",
            "docs/development/chr-lab.md",
            "docs/development/local-environment.md",
            "docs/development/testing.md",
            "docs/operations/recovery.md",
            "CHANGELOG.md",
            "ROADMAP.md",
        ];

        foreach (string relative in relativePaths)
        {
            Assert.True(
                File.Exists(Path.Combine(RepoRoot, relative)),
                $"Missing documentation file: {relative}");
        }
    }

    [Fact]
    public void AcceptanceReportCoversRequiredDoDSections()
    {
        string text = File.ReadAllText(
            Path.Combine(RepoRoot, "docs", "development", "m1-vertical-slice-acceptance.md"));
        Assert.Contains("M1 CLOSED", text, StringComparison.Ordinal);
        Assert.Contains("Known limitations", text, StringComparison.Ordinal);
        Assert.Contains("Clean-environment release candidate", text, StringComparison.Ordinal);
        Assert.Contains("FullyQualifiedName~StandaloneVerticalSlice", text, StringComparison.Ordinal);
        Assert.Contains("FullyQualifiedName~MultiWan", text, StringComparison.Ordinal);
        Assert.Contains("FullyQualifiedName~VrrpVerticalSlice", text, StringComparison.Ordinal);
        Assert.Contains("FullyQualifiedName~FaultInjection", text, StringComparison.Ordinal);
        Assert.Contains("ArchitectureBoundary", text, StringComparison.Ordinal);
        Assert.Contains("vulnerable", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangelogRecordsM1ClosedMilestone()
    {
        string text = File.ReadAllText(Path.Combine(RepoRoot, "CHANGELOG.md"));
        Assert.Contains("M1 Closed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesktopAssembliesDoNotReferenceRouterOs()
    {
        // ADR 0005 / AC#9: Desktop must not take a project reference on Mfc.RouterOs.
        string desktopCsproj = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "Mfc.Desktop", "Mfc.Desktop.csproj"));
        Assert.DoesNotContain("Mfc.RouterOs", desktopCsproj, StringComparison.Ordinal);
        Assert.Contains("Mfc.Contracts", desktopCsproj, StringComparison.Ordinal);
    }
}
