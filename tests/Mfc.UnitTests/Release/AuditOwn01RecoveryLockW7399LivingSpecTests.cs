using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-399: AUDIT-OWN-01 — onboarding Start holds a durable Node lease;
/// background recovery skips live leases.
/// </summary>
public sealed class AuditOwn01RecoveryLockW7399LivingSpecTests
{
    [Fact]
    public void Ac1OwnershipGateAndQueueLockAuditOwn01()
    {
        string root = RepoRoot();
        string ownership = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Onboarding/OnboardingOwnership.cs"));
        string lockType = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Onboarding/OnboardingLock.cs"));
        string recover = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Jobs/RecoverNonterminalOperationsJobUseCase.cs"));
        string start = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Onboarding/OnboardingWorkflowUseCases.cs"));
        string store = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Abstractions/Persistence/OnboardingStores.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("AUDIT-OWN-01", ownership, StringComparison.Ordinal);
        Assert.Contains("IsAbandonedForRecovery", ownership, StringComparison.Ordinal);
        Assert.Contains("RecoverySkippedLockHeld", ownership, StringComparison.Ordinal);
        Assert.Contains("ONBOARDING_LOCK_HELD", ownership, StringComparison.Ordinal);
        Assert.Contains("AcquireForStart", ownership, StringComparison.Ordinal);
        Assert.Contains("Expire", lockType, StringComparison.Ordinal);

        Assert.Contains("RecoverySkippedLockHeld", recover, StringComparison.Ordinal);
        Assert.Contains("OnboardingOwnership.IsAbandonedForRecovery", recover, StringComparison.Ordinal);
        Assert.Contains("AcquireForStart", start, StringComparison.Ordinal);
        Assert.Contains("ExpireOwnedLockAsync", start, StringComparison.Ordinal);
        Assert.Contains("AddLockAsync", store, StringComparison.Ordinal);
        Assert.Contains("ReplaceExpiredLockAsync", store, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-399 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditOwn01RecoveryLockW7399LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-400", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-COMMIT-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-399 | [#1203](https://github.com/sesquicadaver/MTDirector/issues/1203) | AUDIT-OWN-01 — Onboarding durable writer lease vs recovery | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-400 | [#1205](https://github.com/sesquicadaver/MTDirector/issues/1205) | Seed next after AUDIT-OWN-01 → AUDIT-COMMIT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-401 | [#1206](https://github.com/sesquicadaver/MTDirector/issues/1206) | AUDIT-COMMIT-01 — Commit snapshot + journal persist | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-OWN-01 W7-399 (#1203) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-400 (#1205) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-COMMIT-01", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-OWN-01 W7-399 (#1203) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditOwn01RecoveryLockW7399", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7400", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-399` | #1203 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-400` | #1205 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-401` | #1206 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-407 (#1215)", issues, StringComparison.Ordinal);
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
