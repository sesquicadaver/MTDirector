using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-415: AUDIT-AN-03 — server-owned analysis (audit F06).</summary>
public sealed class AuditAn03ServerOwnedAnalysisW7415LivingSpecTests
{
    [Fact]
    public void Ac1ServerOwnsRiskTestsAndLiveDependencyVector()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string owned = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyServerOwnedAnalysis.cs"));
        string live = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/LivePolicyDependencyFingerprintCalculator.cs"));
        string approvals = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyApprovalUseCases.cs"));
        string bind = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyBindingUseCases.cs"));
        string compile = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs"));
        string proto = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/Protos/mfc/v1/policy.proto"));
        string panel = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/PolicyPanelService.cs"));
        string vm = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs"));

        Assert.Contains("AUDIT-AN-03", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-415", plan62, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan62, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-415 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-03", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-415", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-03", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-415", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditAn03ServerOwnedAnalysisW7415", testing, StringComparison.Ordinal);
        Assert.Contains("PolicyServerOwnedAnalysis.Build", approvals, StringComparison.Ordinal);
        Assert.Contains("OpaqueTestIndeterminateCode", owned, StringComparison.Ordinal);
        Assert.Contains("client PASS is not authoritative", owned, StringComparison.Ordinal);
        Assert.Contains("Never echoes", live, StringComparison.Ordinal);
        Assert.Contains("CompanyBindingHash = slots.Company", live, StringComparison.Ordinal);
        Assert.Contains("ManagementAccessProfileHash = slots.Management", live, StringComparison.Ordinal);
        Assert.Contains("FrozenRunFingerprint = null", approvals, StringComparison.Ordinal);
        Assert.Contains("FrozenRunFingerprint = null", bind, StringComparison.Ordinal);
        Assert.Contains("FrozenRunFingerprint = null", compile, StringComparison.Ordinal);
        Assert.Contains("Uuid node_id = 7", proto, StringComparison.Ordinal);
        Assert.Contains("Uuid node_id = 6", proto, StringComparison.Ordinal);
        Assert.Contains("Guid? nodeId = null", panel, StringComparison.Ordinal);
        Assert.Contains("TryGetComposeNodeId()", vm, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FrozenRunFingerprint = run.DependencyFingerprint",
            approvals,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FrozenRunFingerprint = run.DependencyFingerprint",
            bind,
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
