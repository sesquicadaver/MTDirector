using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-438: PLAN63-DONE-01 — PLAN-63 COMPLETE; handoff wave B Layer C; freeze NEXT=none.
/// </summary>
public sealed class Plan63Done01CompleteHandoffW7438LivingSpecTests
{
    [Fact]
    public void Ac1Plan63CompleteAndQueueExhaustedWithWaveBHandoff()
    {
        string root = RepoRoot();
        string plan63 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string readiness = File.ReadAllText(Path.Combine(root, "docs/release/readiness.md"));
        string slash = File.ReadAllText(Path.Combine(root, ".cursor/rules/slash-autopilot.mdc"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));

        Assert.Contains("**Status:** **COMPLETE**", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-438 (#1264) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("PLAN63-DONE-01", plan63, StringComparison.Ordinal);
        Assert.Contains("Handoff freeze", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan63, StringComparison.Ordinal);
        Assert.Contains("PLAN-64", plan63, StringComparison.Ordinal);
        Assert.Contains("черга вичерпана", plan63, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-438 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-63 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN63-DONE-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-438 | [#1264](https://github.com/sesquicadaver/MTDirector/issues/1264) | PLAN63-DONE-01 — PLAN-63 COMPLETE; handoff wave B Layer C | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **0** |", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("§3.C NEXT = W7-438 (#1264)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-63", continuous, StringComparison.Ordinal);
        Assert.Contains("**COMPLETE**", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-438 (#1264) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", readme, StringComparison.Ordinal);
        Assert.Contains("PLAN-63 **COMPLETE**", readme, StringComparison.Ordinal);
        Assert.Contains("PLAN-63 COMPLETE", readiness, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", readiness, StringComparison.Ordinal);

        Assert.Contains("Plan63Done01CompleteHandoffW7438", testing, StringComparison.Ordinal);
        Assert.Contains("черга вичерпана", slash, StringComparison.Ordinal);
        Assert.Contains("не вигадує", slash, StringComparison.Ordinal);
        Assert.Contains("W7-438", changelog, StringComparison.Ordinal);
        Assert.Contains("PLAN63-DONE-01", changelog, StringComparison.Ordinal);
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
