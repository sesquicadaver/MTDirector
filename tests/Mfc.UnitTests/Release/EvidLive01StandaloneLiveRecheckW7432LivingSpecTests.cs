using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-432: EVID-LIVE-01 — standalone live Recheck before staging (F02 residual).
/// </summary>
public sealed class EvidLive01StandaloneLiveRecheckW7432LivingSpecTests
{
    [Fact]
    public void Ac1ProductionLiveRecheckWiredBeforeStaging()
    {
        string root = RepoRoot();
        string live = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Deployment/StandaloneLiveRecheck.cs"));
        string execute = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs"));
        string coverage = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Deployment/StandaloneDeploymentLivingSpecTests.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan63 = File.ReadAllText(
            Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("StandaloneLiveRecheck", live, StringComparison.Ordinal);
        Assert.Contains("ReadManagedStateAsync", live, StringComparison.Ordinal);
        Assert.Contains("OldAnchorTargets", live, StringComparison.Ordinal);
        Assert.Contains("ReadManagedResourceHashAsync", live, StringComparison.Ordinal);
        Assert.Contains("StandaloneLiveRecheck.ExecuteAsync", execute, StringComparison.Ordinal);
        Assert.Contains("precheck:live-ros", execute, StringComparison.Ordinal);
        Assert.Contains("EVID-LIVE-01", execute, StringComparison.Ordinal);
        Assert.Contains("Ac1bLiveRecheckRejectsDivergedOldAnchors", coverage, StringComparison.Ordinal);
        Assert.Contains("precheck:live-ros", coverage, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-432 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("EVID-LIVE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-432 | [#1257](https://github.com/sesquicadaver/MTDirector/issues/1257) | EVID-LIVE-01 — Standalone live Recheck preconditions (F02 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-436 (#1261)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **3** |", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-432 (#1257) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("EVID-LIVE-01", plan63, StringComparison.Ordinal);
        Assert.Contains("EvidLive01StandaloneLiveRecheckW7432", testing, StringComparison.Ordinal);
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
