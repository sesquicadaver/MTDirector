using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-419: AUDIT-GUI-02 — Controller onboarding + stale policy GUI (audit F11).</summary>
public sealed class AuditGui02ControllerOnboardingStalePolicyW7419LivingSpecTests
{
    [Fact]
    public void Ac1ControllerBuildsOnboardingFromLastCaptureAndPoliciesClearStaleSafety()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string codes = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Onboarding/OnboardingCodes.cs"));
        string fromCapture = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Onboarding/CreateOnboardingPlanFromLastCaptureUseCase.cs"));
        string workflow = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Onboarding/OnboardingWorkflowUseCases.cs"));
        string grpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingGrpcService.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string onboardingVm = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs"));
        string policiesVm = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs"));
        string policiesTests = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Desktop/PoliciesViewModelTests.cs"));
        string onboardingTests = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Desktop/OnboardingViewModelTests.cs"));

        Assert.Contains("AUDIT-GUI-02", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-419 (#1236) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-419 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-02 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-419", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUI-02", roadmap, StringComparison.Ordinal);
        Assert.Contains("**DONE**", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-419", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditGui02ControllerOnboardingStalePolicyW7419", testing, StringComparison.Ordinal);
        Assert.Contains("CaptureRequired", codes, StringComparison.Ordinal);
        Assert.Contains("ONBOARDING_CAPTURE_REQUIRED", codes, StringComparison.Ordinal);
        Assert.Contains("CreateOnboardingPlanFromLastCaptureUseCase", fromCapture, StringComparison.Ordinal);
        Assert.Contains("command.Facts.Count == 0", workflow, StringComparison.Ordinal);
        Assert.Contains("OnboardingCodes.CaptureRequired", workflow, StringComparison.Ordinal);
        Assert.Contains("request.Devices.Count == 0", grpc, StringComparison.Ordinal);
        Assert.Contains("_createPlanFromLastCapture", grpc, StringComparison.Ordinal);
        Assert.Contains("CreateOnboardingPlanFromLastCaptureUseCase", program, StringComparison.Ordinal);
        Assert.Contains("no longer fabricates DefaultFacts", onboardingVm, StringComparison.Ordinal);
        Assert.Contains("Controller-built", onboardingVm, StringComparison.Ordinal);
        Assert.DoesNotContain("ONBOARDING_FACTS_REQUIRED", onboardingVm, StringComparison.Ordinal);
        Assert.Contains("ClearStaleSafetySurfaces", policiesVm, StringComparison.Ordinal);
        Assert.Contains("_safetyAnalysisGeneration", policiesVm, StringComparison.Ordinal);
        Assert.Contains("SelectedCatalogItem = Catalog.FirstOrDefault", policiesVm, StringComparison.Ordinal);
        Assert.Contains("SwitchingInventoryDeviceClearsStaleSafetyFindings", policiesTests, StringComparison.Ordinal);
        Assert.Contains("CreatePlanRequestsEmptyDevicesForControllerBuiltLastCapturePlan", onboardingTests, StringComparison.Ordinal);
        string fromCaptureTests = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Application/CreateOnboardingPlanFromLastCaptureUseCaseTests.cs"));
        Assert.Contains("FromLastCaptureBuildsPlanFromSnapshotHashes", fromCaptureTests, StringComparison.Ordinal);
        Assert.Contains("EmptyValidateFactsWithoutCaptureReturnCaptureRequiredBlocker", fromCaptureTests, StringComparison.Ordinal);
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
