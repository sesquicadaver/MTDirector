using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-222: AUDIT-DIFF-01 semantic policy diff surfaces reachability change.</summary>
public sealed class AuditDiff01SemanticReachabilityW7222LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndDifferLockAuditDiff01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string differ = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs"));
        string review = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyReviewUseCases.cs"));
        string risk = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Policy/PolicyRiskClassifier.cs"));

        Assert.Contains("AUDIT-DIFF-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-222", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-222 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DIFF-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-222", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DIFF-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-222", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditDiff01SemanticReachabilityW7222", testing, StringComparison.Ordinal);
        Assert.Contains("FirstMatchAcceptedSpace", differ, StringComparison.Ordinal);
        Assert.Contains("beforeAddresses", differ, StringComparison.Ordinal);
        Assert.Contains("ChainDefaultDispositionChanged", differ, StringComparison.Ordinal);
        Assert.Contains("DefaultDispositionChanged", review, StringComparison.Ordinal);
        Assert.Contains("ProofIndeterminate", risk, StringComparison.Ordinal);
        Assert.DoesNotContain("UnionEffects(", differ, StringComparison.Ordinal);
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
