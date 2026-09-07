using System.Text.RegularExpressions;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// QG-DOCS-01 Living Spec: README / docs index stay aligned with ROADMAP §3.C NEXT
/// and critical docs surface files remain present (weekly docs smoke).
/// </summary>
public sealed class QgDocs01WeeklyDocsSmokeLivingSpecTests
{
    private static readonly Regex NextToken =
        new(@"§3\.C NEXT = (W7-\d+ \(#\d+\))", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static string CanonicalNext(string roadmap)
    {
        Match match = NextToken.Match(roadmap);
        Assert.True(match.Success, "ROADMAP.md must declare §3.C NEXT = W7-NN (#issue).");
        return match.Groups[1].Value;
    }

    [Fact]
    public void Ac1RootAndDocsReadmeMatchRoadmapNext()
    {
        string root = RepoRoot();
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));

        string next = CanonicalNext(roadmap);
        string expected = "§3.C NEXT = " + next;

        Assert.Contains(expected, readme, StringComparison.Ordinal);
        Assert.Contains(expected, docsIndex, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsIndexRelativeLinksResolve()
    {
        string root = RepoRoot();
        string docsRoot = Path.Combine(root, "docs");
        string indexPath = Path.Combine(docsRoot, "README.md");
        string index = File.ReadAllText(indexPath);

        foreach (Match match in Regex.Matches(index, @"\]\(([^)#]+\.md)(?:#[^)]*)?\)"))
        {
            string relative = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
            string target = Path.GetFullPath(Path.Combine(docsRoot, relative));
            Assert.True(
                File.Exists(target),
                $"docs/README.md link target missing: {match.Groups[1].Value} → {target}");
        }
    }

    [Fact]
    public void Ac3ServiceSurfaceFilesExist()
    {
        string root = RepoRoot();
        string[] required =
        [
            "ROADMAP.md",
            "ISSUES.md",
            "CHANGELOG.md",
            "Directory.Build.props",
            "MikroTikFirewallController.sln",
            "docs/development/testing.md",
            "docs/release/known-limitations.md",
            "docs/development/docs-smoke.md",
            "docs/planning/plan-03-quality-gates.md",
        ];

        foreach (string relative in required)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Missing service/docs surface file: {relative}");
        }
    }

    [Fact]
    public void Ac4WeeklySmokeChecklistDocumentsRequiredChecks()
    {
        string path = Path.Combine(RepoRoot(), "docs/development/docs-smoke.md");
        string content = File.ReadAllText(path);

        Assert.Contains("QG-DOCS-01", content, StringComparison.Ordinal);
        Assert.Contains("ROADMAP.md", content, StringComparison.Ordinal);
        Assert.Contains("README.md", content, StringComparison.Ordinal);
        Assert.Contains("docs/README.md", content, StringComparison.Ordinal);
        Assert.Contains("QgDocs01WeeklyDocsSmokeLivingSpecTests", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5DocsMatrixDocumentsQgDocs01LivingSpec()
    {
        string testing = File.ReadAllText(Path.Combine(RepoRoot(), "docs/development/testing.md"));
        string plan03 = File.ReadAllText(Path.Combine(RepoRoot(), "docs/planning/plan-03-quality-gates.md"));

        Assert.Contains("QG-DOCS-01", testing, StringComparison.Ordinal);
        Assert.Contains("QgDocs01WeeklyDocsSmokeLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("W7-53 DONE", plan03, StringComparison.Ordinal);
    }
}
