using System.Text.RegularExpressions;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// QG-LIVESPEC-MATRIX-01: DONE PLAN-03 quality-gate IDs must have Living Spec sections in testing.md
/// (ROADMAP §5 / docs matrix stay in sync with delivered gates).
/// </summary>
public sealed class QgLivespecMatrix01LivingSpecTests
{
    private static readonly Regex DoneQgRow =
        new(
            @"\|\s*\d+\s*\|\s*\*\*(QG-[A-Z0-9-]+)\*\*.*\|\s*\*\*W7-\d+\s+DONE\*\*",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TestingLivingSpecHeading =
        new(
            @"^## Living Specification — .+\((W7-\d+)\)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled);

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

    [Fact]
    public void Ac1RoadmapSection5LivingSpecMatrixExists()
    {
        string roadmap = File.ReadAllText(Path.Combine(RepoRoot(), "ROADMAP.md"));
        Assert.Contains("## 5. Living Specification", roadmap, StringComparison.Ordinal);
        Assert.Contains("матриця", roadmap, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DonePlan03QualityGatesHaveTestingMdSections()
    {
        string root = RepoRoot();
        string plan03 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-03-quality-gates.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        MatchCollection done = DoneQgRow.Matches(plan03);
        Assert.True(done.Count >= 3, "PLAN-03 must list completed QG-* DONE rows.");

        foreach (Match match in done)
        {
            string qgId = match.Groups[1].Value;
            Assert.Contains(qgId, testing, StringComparison.Ordinal);
            Assert.Contains("Living Specification — " + qgId, testing, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac3RoadmapDoneQgRowsHaveTestingMdCoverage()
    {
        string root = RepoRoot();
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        // Status table rows: | QG-… | W7-NN | … | **DONE** (#…) |
        Regex roadmapDoneQg = new(
            @"\|\s*(QG-[A-Z0-9-]+)\s+—[^|]+\|\s*(W7-\d+)\s+\|[^|]+\|\s*\*\*DONE\*\*",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        MatchCollection rows = roadmapDoneQg.Matches(roadmap);
        Assert.True(rows.Count >= 3, "ROADMAP §5 status table must include DONE QG rows.");

        foreach (Match match in rows)
        {
            string qgId = match.Groups[1].Value;
            string w7Id = match.Groups[2].Value;
            Assert.Contains(qgId, testing, StringComparison.Ordinal);
            Assert.Contains(w7Id, testing, StringComparison.Ordinal);
            Assert.Contains("Living Specification — " + qgId, testing, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac4RecentQgLivingSpecHeadingsReferenceQueueIds()
    {
        string testing = File.ReadAllText(Path.Combine(RepoRoot(), "docs/development/testing.md"));
        MatchCollection headings = TestingLivingSpecHeading.Matches(testing);
        Assert.Contains(headings, static m => m.Groups[1].Value == "W7-52");
        Assert.Contains(headings, static m => m.Groups[1].Value == "W7-53");
        Assert.Contains(headings, static m => m.Groups[1].Value == "W7-54");
        Assert.Contains(headings, static m => m.Groups[1].Value == "W7-55");
    }

    [Fact]
    public void Ac5DocsMatrixDocumentsQgLivespecMatrix01()
    {
        string root = RepoRoot();
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string plan03 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-03-quality-gates.md"));
        string checklist = File.ReadAllText(Path.Combine(root, "docs/development/livespec-matrix-gate.md"));

        Assert.Contains("QG-LIVESPEC-MATRIX-01", testing, StringComparison.Ordinal);
        Assert.Contains("QgLivespecMatrix01LivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("W7-55 DONE", plan03, StringComparison.Ordinal);
        Assert.Contains("QG-LIVESPEC-MATRIX-01", checklist, StringComparison.Ordinal);
    }
}
