using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-228: AUDIT-DEP-02 — RouterOS clock for watchdog deadline; monotonic remaining; cleanup fail-closed.</summary>
public sealed class AuditDep02WatchdogClockTtlW7228LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndRuntimeGateLockAuditDep02()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string runtime = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs"));
        string writer = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/DeploymentWatchdogWriter.cs"));
        string standalone = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs"));
        string recovery = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/RecoverDeploymentUseCase.cs"));
        string session = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs"));
        string vrrp = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs"));

        Assert.Contains("AUDIT-DEP-02", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-228", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-228 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-02", limitations, StringComparison.Ordinal);
        Assert.Contains("ReadRouterClockAsync", limitations, StringComparison.Ordinal);
        Assert.Contains("WatchdogTimeBudget", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-228", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-02", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-228", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditDep02WatchdogClockTtlW7228", testing, StringComparison.Ordinal);

        Assert.Contains("ReadRouterClockAsync", runtime, StringComparison.Ordinal);
        Assert.Contains("routerClock", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("UtcDateTime", writer, StringComparison.Ordinal);
        Assert.Contains("WatchdogTimeBudget", standalone, StringComparison.Ordinal);
        Assert.Contains("budget.Remaining", standalone, StringComparison.Ordinal);
        Assert.Contains("cleaned.Succeeded", recovery, StringComparison.Ordinal);
        Assert.Contains("RouterOsClockParser.Parse", session, StringComparison.Ordinal);
        Assert.Contains("WatchdogTimeBudget", vrrp, StringComparison.Ordinal);

        Assert.NotNull(typeof(WatchdogTimeBudget).GetConstructor([typeof(TimeSpan)]));
        Assert.NotNull(typeof(RouterOsClockParser).GetMethod(
            nameof(RouterOsClockParser.Parse),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(IStandaloneDeploymentDeviceRuntime).GetMethod(
            nameof(IStandaloneDeploymentDeviceRuntime.ReadRouterClockAsync)));
        MethodInfo? disarm = typeof(IDeploymentRollbackDeviceRuntime).GetMethod(
            nameof(IDeploymentRollbackDeviceRuntime.DisarmAndCleanupWatchdogAsync));
        Assert.NotNull(disarm);
        Assert.Equal(typeof(Task<DeploymentWatchdogExecutionResult>), disarm!.ReturnType);
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
