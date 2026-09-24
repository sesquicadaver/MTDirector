using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-427: PLAN62-DONE-01 — PLAN-62 COMPLETE; §3.C NEXT=none freeze (ROADMAP §6).
/// </summary>
public sealed class Plan62Done01CompleteFreezeW7427LivingSpecTests
{
    [Fact]
    public void Ac1Plan62CompleteAndNextIsNone()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string slash = File.ReadAllText(Path.Combine(root, ".cursor/rules/slash-autopilot.mdc"));

        Assert.Contains("**Status:** **COMPLETE**", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-427 (#1248) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("PLAN62-DONE-01", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", plan62, StringComparison.Ordinal);
        Assert.DoesNotContain("OPEN (NEXT)", plan62, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-427 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-62 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", limitations, StringComparison.Ordinal);
        Assert.Contains("черга вичерпана", limitations, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "W7-427 | [#1248](https://github.com/sesquicadaver/MTDirector/issues/1248) | PLAN62-DONE-01 — PLAN-62 COMPLETE + freeze NEXT=none | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-62", continuous, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none", readme, StringComparison.Ordinal);

        Assert.Contains("Plan62Done01CompleteFreezeW7427", testing, StringComparison.Ordinal);
        Assert.Contains("черга вичерпана", slash, StringComparison.Ordinal);
        Assert.Contains("не вигадує", slash, StringComparison.Ordinal);
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
