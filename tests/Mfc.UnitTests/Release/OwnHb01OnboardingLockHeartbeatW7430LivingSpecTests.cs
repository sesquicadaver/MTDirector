using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-430: OWN-HB-01 — onboarding lock heartbeat mirrors deployment (F01 residual).
/// </summary>
public sealed class OwnHb01OnboardingLockHeartbeatW7430LivingSpecTests
{
    [Fact]
    public void Ac1ProductionHeartbeatJobAndLockTickWiringExist()
    {
        string root = RepoRoot();
        string useCase = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Jobs/HeartbeatOnboardingLocksJobUseCase.cs"));
        string store = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Abstractions/Persistence/OnboardingStores.cs"));
        string ef = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Onboarding/EfOnboardingStore.cs"));
        string executor = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Controller/Jobs/OperationalJobExecutor.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string coverage = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Jobs/OperationalJobUseCaseCoverageTests.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan63 = File.ReadAllText(
            Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("HeartbeatOnboardingLocksJobUseCase", useCase, StringComparison.Ordinal);
        Assert.Contains("ListLocksByOwnerAsync", useCase, StringComparison.Ordinal);
        Assert.Contains("ListLocksByOwnerAsync", store, StringComparison.Ordinal);
        Assert.Contains("ListLocksByOwnerAsync", ef, StringComparison.Ordinal);
        Assert.Contains("HeartbeatOnboardingLocksJobUseCase", executor, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", executor, StringComparison.Ordinal);
        Assert.Contains("AddScoped<HeartbeatOnboardingLocksJobUseCase>", program, StringComparison.Ordinal);
        Assert.Contains("OnboardingHeartbeatRefreshesOwnedNonExpiredLocks", coverage, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-430 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-430 | [#1255](https://github.com/sesquicadaver/MTDirector/issues/1255) | OWN-HB-01 — Onboarding lock heartbeat (F01 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-438 (#1264)", roadmap, StringComparison.Ordinal);
        Assert.Contains("OWN-HB-01", plan63, StringComparison.Ordinal);
        Assert.Contains("W7-430 (#1255) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("OwnHb01OnboardingLockHeartbeatW7430", testing, StringComparison.Ordinal);
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
