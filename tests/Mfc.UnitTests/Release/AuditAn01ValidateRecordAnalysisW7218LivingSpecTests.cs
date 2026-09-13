using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-218: AUDIT-AN-01 Validate/Record require structural analysis + mandatory test coverage; Compose→Record WARNING.</summary>
public sealed class AuditAn01ValidateRecordAnalysisW7218LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndGatesLockAuditAn01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string validate = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/ValidateRevisionUseCase.cs"));
        string structural = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyRevisionStructuralAnalysis.cs"));
        string approvals = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyApprovalUseCases.cs"));
        string gate = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Policy/PolicyApprovalGate.cs"));
        string panel = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/PolicyPanelService.cs"));
        string codes = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Policy/PolicyApprovalCodes.cs"));

        Assert.Contains("AUDIT-AN-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-218", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-218 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-218", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-218", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditAn01ValidateRecordAnalysisW7218", testing, StringComparison.Ordinal);
        Assert.Contains("EnsureStructuralAnalysisPasses", validate, StringComparison.Ordinal);
        Assert.Contains("EnsureTestCoverage", structural, StringComparison.Ordinal);
        Assert.Contains("EnsureTestCoverage", approvals, StringComparison.Ordinal);
        Assert.Contains("NormalizeFindingSeverity", approvals, StringComparison.Ordinal);
        Assert.Contains("TestsIncomplete", gate, StringComparison.Ordinal);
        Assert.Contains("TestsIncomplete", codes, StringComparison.Ordinal);
        Assert.Contains("NormalizeRecordSeverity", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Severity = \"INFO\"", panel, StringComparison.Ordinal);
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
