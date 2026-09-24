using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-421: AUDIT-DRIFT-01 — drift from live RouterOS read (audit F12).</summary>
public sealed class AuditDrift01LiveRouterOsReadW7421LivingSpecTests
{
    [Fact]
    public void Ac1PollUsesLiveReadPortAndFailedReadIsNotNoDrift()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string poll = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Jobs/PollManagedDriftJobUseCase.cs"));
        string detect = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Drift/DriftUseCases.cs"));
        string port = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Abstractions/Jobs/IManagedDriftLiveReadPort.cs"));
        string rosPort = File.ReadAllText(
            Path.Combine(root, "src/Mfc.RouterOs/Jobs/RouterOsManagedDriftLiveReadPort.cs"));
        string di = File.ReadAllText(
            Path.Combine(root, "src/Mfc.RouterOs/DependencyInjection/RouterOsServiceCollectionExtensions.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string coverage = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Jobs/OperationalJobUseCaseCoverageTests.cs"));

        Assert.Contains("AUDIT-DRIFT-01", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-421 (#1239) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-421 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DRIFT-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-421", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DRIFT-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("**DONE**", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-421", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditDrift01LiveRouterOsReadW7421", testing, StringComparison.Ordinal);

        Assert.Contains("IManagedDriftLiveReadPort", poll, StringComparison.Ordinal);
        Assert.Contains("IgnorePersistedActual = true", poll, StringComparison.Ordinal);
        Assert.Contains("PersistActualHash = true", poll, StringComparison.Ordinal);
        Assert.Contains("DriftFindingKind.ManagedRuleChanged", poll, StringComparison.Ordinal);
        Assert.Contains("IgnorePersistedActual", detect, StringComparison.Ordinal);
        Assert.Contains("ReadActualManagedResourceAsync", port, StringComparison.Ordinal);
        Assert.Contains("NotConfiguredManagedDriftLiveReadPort", port, StringComparison.Ordinal);
        Assert.Contains("ReadManagedStateAsync", rosPort, StringComparison.Ordinal);
        Assert.Contains("ManagedResourceHashObservation.TryComputeFromManagedState", rosPort, StringComparison.Ordinal);
        Assert.Contains("AddScoped<IManagedDriftLiveReadPort, RouterOsManagedDriftLiveReadPort>", di, StringComparison.Ordinal);
        Assert.Contains("RegisterProductionServices", di, StringComparison.Ordinal);
        Assert.Contains("TryAddSingleton<IManagedDriftLiveReadPort, NotConfiguredManagedDriftLiveReadPort>", program, StringComparison.Ordinal);
        Assert.Contains("PollManagedDriftFailedLiveReadIsNotNoDrift", coverage, StringComparison.Ordinal);
        Assert.Contains("MatchingLiveReadPort", coverage, StringComparison.Ordinal);
        Assert.Contains("FailingLiveReadPort", coverage, StringComparison.Ordinal);
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
