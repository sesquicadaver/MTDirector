using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-212: AUDIT-CTX-01 node switch invalidates Zones/Onboarding/Deployment mutation context.</summary>
public sealed class AuditCtx01NodeSwitchInvalidateW7212LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndCodeLockAuditCtx01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string zones = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/ZonesViewModel.cs"));
        string onboarding = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs"));
        string deployment = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs"));

        Assert.Contains("AUDIT-CTX-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-212", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-212 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-212", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CTX-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-212", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditCtx01NodeSwitchInvalidateW7212", testing, StringComparison.Ordinal);
        Assert.Contains("SyncMutationOwnerFromSelection", zones, StringComparison.Ordinal);
        Assert.Contains("InvalidateNodeScopedMutationContext", zones, StringComparison.Ordinal);
        Assert.Contains("SyncMutationOwnerFromSelection", onboarding, StringComparison.Ordinal);
        Assert.Contains("InvalidateMutationContext", onboarding, StringComparison.Ordinal);
        Assert.Contains("SyncMutationOwnerFromSelection", deployment, StringComparison.Ordinal);
        Assert.Contains("InvalidateMutationContext", deployment, StringComparison.Ordinal);
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
