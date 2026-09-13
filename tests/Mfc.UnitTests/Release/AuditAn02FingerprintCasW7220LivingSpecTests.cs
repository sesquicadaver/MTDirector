using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-220: AUDIT-AN-02 Controller computes dependency fingerprint; client hash is CAS expectation only.</summary>
public sealed class AuditAn02FingerprintCasW7220LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndCasHostLockAuditAn02()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string cas = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyDependencyFingerprintCas.cs"));
        string live = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/LivePolicyDependencyFingerprintCalculator.cs"));
        string compile = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs"));
        string approvals = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyApprovalUseCases.cs"));
        string bind = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyBindingUseCases.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string proto = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/Protos/mfc/v1/policy.proto"));
        string panel = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/PolicyPanelService.cs"));
        string vm = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs"));

        Assert.Contains("AUDIT-AN-02", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-220", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-220 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-220", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-02", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-220", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditAn02FingerprintCasW7220", testing, StringComparison.Ordinal);
        Assert.Contains("PolicyDependencyFingerprintCas.Evaluate", compile, StringComparison.Ordinal);
        Assert.Contains("PolicyDependencyFingerprintCas.Evaluate", approvals, StringComparison.Ordinal);
        Assert.Contains("PolicyDependencyFingerprintCas.Evaluate", bind, StringComparison.Ordinal);
        Assert.Contains("server-computed", cas, StringComparison.Ordinal);
        Assert.Contains("LivePolicyDependencyFingerprintCalculator", live, StringComparison.Ordinal);
        Assert.Contains("LivePolicyDependencyFingerprintCalculator", program, StringComparison.Ordinal);
        Assert.Contains("Uuid node_id = 18", proto, StringComparison.Ordinal);
        Assert.Contains("NodeId", approvals, StringComparison.Ordinal);
        Assert.Contains("nodeId", panel, StringComparison.Ordinal);
        Assert.Contains("ComposeNodeIdText", vm, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "analysisCurrent = run.DependencyFingerprint.Equals(clientExpectedFingerprint",
            compile,
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
