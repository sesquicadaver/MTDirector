using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// Locks the queue rule: a non-empty NEXT is not a license to invent the next row.
/// When the following NEXT is empty, work stops and exhaustion is reported.
/// </summary>
public sealed class QueueExhaustionNextStopLivingSpecTests
{
    [Fact]
    public void Ac1EmptyNextStopsAndDoesNotSeed()
    {
        string root = RepoRoot();
        string rule = File.ReadAllText(Path.Combine(root, ".cursor/rules/slash-autopilot.mdc"));
        string contributing = File.ReadAllText(Path.Combine(root, "CONTRIBUTING.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("черга вичерпана", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("не простоювати", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("засіяти наступний атомарний рядок", rule, StringComparison.Ordinal);

        Assert.Contains("queue is exhausted", contributing, StringComparison.Ordinal);
        Assert.DoesNotContain("seed the next tranche", contributing, StringComparison.Ordinal);

        Assert.Contains("Rule (queue exhaustion; supersedes PLAN-02 self-seed)", plan, StringComparison.Ordinal);
        Assert.Contains("reports that the queue is exhausted", plan, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "closing a delivery wave without seeding the next §3 row in the same cycle is forbidden",
            plan,
            StringComparison.Ordinal);
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
