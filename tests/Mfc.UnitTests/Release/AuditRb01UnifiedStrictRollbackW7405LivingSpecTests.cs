using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-405: AUDIT-RB-01 — unified strict rollback for automatic/explicit/recovery.
/// </summary>
public sealed class AuditRb01UnifiedStrictRollbackW7405LivingSpecTests
{
    [Fact]
    public void Ac1AutomaticAndVrrpUseStrictRollbackCoordinator()
    {
        string root = RepoRoot();
        string standalone = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs"));
        string recover = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Deployment/RecoverDeploymentUseCase.cs"));
        string vrrp = File.ReadAllText(Path.Combine(
            root, "src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs"));
        string live = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Deployment/IDeploymentLiveDeviceSession.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("IStandaloneDeploymentDeviceRuntime : IDeploymentRollbackDeviceRuntime", standalone, StringComparison.Ordinal);
        Assert.Contains("ExecuteDeploymentRollbackUseCase.ExecuteAsync", standalone, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RB-01", standalone, StringComparison.Ordinal);
        Assert.DoesNotContain("watchdog:disarmed-on-rollback", standalone, StringComparison.Ordinal);

        Assert.Contains("RollbackDeviceAsync", recover, StringComparison.Ordinal);
        Assert.Contains("third/manual anchor target blocks automatic rollback", recover, StringComparison.Ordinal);
        Assert.Contains("watchdog disarm/cleanup failed during strict rollback", recover, StringComparison.Ordinal);

        Assert.Contains("ExecuteDeploymentRollbackUseCase.RollbackDeviceAsync", vrrp, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = await _device.Watchdog.DisarmWatchdogAsync(_armed", vrrp, StringComparison.Ordinal);

        Assert.Contains("IStandaloneDeploymentDeviceRuntime", live, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-405 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RB-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditRb01UnifiedStrictRollbackW7405LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-407", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CLK-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-405 | [#1212](https://github.com/sesquicadaver/MTDirector/issues/1212) | AUDIT-RB-01 — Unified strict rollback | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-406 | [#1214](https://github.com/sesquicadaver/MTDirector/issues/1214) | Seed next after AUDIT-RB-01 → AUDIT-CLK-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-407 | [#1215](https://github.com/sesquicadaver/MTDirector/issues/1215) | AUDIT-CLK-01 — RouterOS clock / TTL budget | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-425 (#1245)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-RB-01 W7-405 (#1212) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-406 (#1214) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-425 (#1245)", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CLK-01", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-RB-01 W7-405 (#1212) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-425 (#1245)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditRb01UnifiedStrictRollbackW7405", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7406", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-405` | #1212 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-406` | #1214 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-407` | #1215 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-425 (#1245)", issues, StringComparison.Ordinal);
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
