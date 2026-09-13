using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-226: AUDIT-DEP-01 recovery must not race an active deployment lock.</summary>
public sealed class AuditDep01RecoveryLockW7226LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndOwnershipGateLockAuditDep01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string ownership = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Deployment/DeploymentOwnership.cs"));
        string recover = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Jobs/RecoverNonterminalOperationsJobUseCase.cs"));
        string start = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs"));

        Assert.Contains("AUDIT-DEP-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-226", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-226 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-226", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-226", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditDep01RecoveryLockW7226", testing, StringComparison.Ordinal);
        Assert.Contains("IsAbandonedForRecovery", ownership, StringComparison.Ordinal);
        Assert.Contains("RecoverySkippedLockHeld", ownership, StringComparison.Ordinal);
        Assert.Contains("DEPLOYMENT_LOCK_HELD", ownership, StringComparison.Ordinal);
        Assert.Contains("RecoverySkippedLockHeld", recover, StringComparison.Ordinal);
        Assert.Contains("AcquireForStart", start, StringComparison.Ordinal);
        Assert.Contains("ReplaceExpiredLockAsync", start, StringComparison.Ordinal);
        Assert.Contains("AddStepAsync", start, StringComparison.Ordinal);
        Assert.Contains("DeploymentStep.Create", start, StringComparison.Ordinal);
        Assert.Contains("ExpireOwnedLockAsync", start, StringComparison.Ordinal);
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
