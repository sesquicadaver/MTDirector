using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-210: AUDIT-RULE-01 Update rule preserves predicate / logging / exceptionEligible.</summary>
public sealed class AuditRule01PredicateRoundTripW7210LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndTestingDocLockAuditRule01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string panel = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/PolicyPanelService.cs"));
        string vm = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs"));
        string grpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/GrpcPolicyServiceClient.cs"));

        Assert.Contains("AUDIT-RULE-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-210", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-210 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RULE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-210", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RULE-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-210", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditRule01PredicateRoundTripW7210", testing, StringComparison.Ordinal);
        Assert.Contains("ExceptionEligible = rule.ExceptionEligible", panel, StringComparison.Ordinal);
        Assert.Contains("ResolvePredicateForUpdate", vm, StringComparison.Ordinal);
        Assert.Contains("ExceptionEligible = exceptionEligible", grpc, StringComparison.Ordinal);
        Assert.Contains("Logging = logging.Clone()", grpc, StringComparison.Ordinal);
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
