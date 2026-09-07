using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-51: PLAN-03 quality-gate inventory documents ranked QG rows and seeds QG-IMPORT-01.</summary>
public sealed class Plan03QualityGatesW751LivingSpecTests
{
    [Fact]
    public void Ac1Plan03InventoryDocumentsRankedQualityGatesAndSeedsImportGraph()
    {
        string root = RepoRoot();
        string plan03 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-03-quality-gates.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("PLAN-03 — Quality-gate product tranche", plan03, StringComparison.Ordinal);
        Assert.Contains("QG-IMPORT-01", plan03, StringComparison.Ordinal);
        Assert.Contains("QG-DOCS-01", plan03, StringComparison.Ordinal);
        Assert.Contains("QG-ANTISTUB-01", plan03, StringComparison.Ordinal);
        Assert.Contains("W7-52 OPEN", plan03, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-51 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-03", limitations, StringComparison.Ordinal);
        Assert.Contains("QG-IMPORT-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-52", continuous, StringComparison.Ordinal);
    }

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
}
