using System.Reflection;
using Mfc.Application.Onboarding;
using Mfc.Domain.Deployment;
using Mfc.Domain.Onboarding;
using Mfc.RouterOs.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-407: AUDIT-CLK-01 — onboarding watchdog deadline from RouterOS clock + remaining TTL.
/// </summary>
public sealed class AuditClk01RouterOsClockTtlW7407LivingSpecTests
{
    [Fact]
    public void Ac1OnboardingReadsRouterClockAndTracksRemainingTtl()
    {
        string root = RepoRoot();
        string execute = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Onboarding/ExecuteOnboardingBootstrapUseCase.cs"));
        string start = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Onboarding/OnboardingWorkflowUseCases.cs"));
        string runtime = File.ReadAllText(Path.Combine(
            root, "src/Mfc.RouterOs/Onboarding/RouterOsOnboardingRuntime.cs"));
        string session = File.ReadAllText(Path.Combine(
            root, "src/Mfc.RouterOs/Onboarding/RouterOsOnboardingDeviceSession.cs"));
        string writer = File.ReadAllText(Path.Combine(
            root, "src/Mfc.RouterOs/Onboarding/OnboardingWatchdogWriter.cs"));
        string codes = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Domain/Onboarding/OnboardingCodes.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("ReadRouterClockAsync", execute, StringComparison.Ordinal);
        Assert.Contains("WatchdogTimeBudget", execute, StringComparison.Ordinal);
        Assert.Contains("OnboardingCodes.RouterClockSkew", execute, StringComparison.Ordinal);
        Assert.Contains("budgets[devicePlan.DeviceId].Remaining", execute, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CLK-01", execute, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CLK-01", start, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteAsync(node, plan, operation, now, now,", start, StringComparison.Ordinal);
        Assert.Contains("ExecuteAsync(node, plan, operation, now, cancellationToken)", start, StringComparison.Ordinal);

        Assert.DoesNotContain("DateTimeOffset routerClock", runtime, StringComparison.Ordinal);
        Assert.Contains("ReadRouterClockAsync", session, StringComparison.Ordinal);
        Assert.Contains("RouterOsClockParser.Parse", session, StringComparison.Ordinal);

        Assert.Contains("routerClock + remaining", writer, StringComparison.Ordinal);
        Assert.DoesNotContain("routerClock + bundle.Ttl", writer, StringComparison.Ordinal);

        Assert.Contains("RouterClockSkew", codes, StringComparison.Ordinal);
        Assert.Contains("MaxRouterClockSkew", codes, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromMinutes(5), OnboardingCodes.MaxRouterClockSkew);
        Assert.NotNull(typeof(IOnboardingDeviceSession).GetMethod(nameof(IOnboardingDeviceSession.ReadRouterClockAsync)));
        Assert.NotNull(typeof(WatchdogTimeBudget).GetConstructor([typeof(TimeSpan)]));

        Assert.Contains("Intentional residual (W7-407 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CLK-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditClk01RouterOsClockTtlW7407LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-408", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-407 | [#1215](https://github.com/sesquicadaver/MTDirector/issues/1215) | AUDIT-CLK-01 — RouterOS clock / TTL budget | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-408 | [#1217](https://github.com/sesquicadaver/MTDirector/issues/1217) | Seed next after AUDIT-CLK-01 → AUDIT-RPC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-409 | [#1218](https://github.com/sesquicadaver/MTDirector/issues/1218) | AUDIT-RPC-01 — Fast Start + live Watch | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CLK-01 W7-407 (#1215) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-408 (#1217) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RPC-01 W7-409 (#1218) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CLK-01 W7-407 (#1215) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditClk01RouterOsClockTtlW7407", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7408", testing, StringComparison.Ordinal);
        Assert.Contains("AuditRpc01FastStartLiveWatchW7409", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-407` | #1215 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-408` | #1217 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-409` | #1218 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", issues, StringComparison.Ordinal);
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
