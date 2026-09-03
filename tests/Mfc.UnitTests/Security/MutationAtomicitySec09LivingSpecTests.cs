using Mfc.Application.Abstractions.Persistence;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-09 (#387) — UoW for onboarding workflow terminal writes.</summary>
public sealed class MutationAtomicitySec09LivingSpecTests
{
    [Fact]
    public void Ac1OnboardingWorkflowSourcesUseUnitOfWorkBoundary()
    {
        string path = Path.Combine(
            FindRepoRoot(), "src", "Mfc.Application", "Onboarding", "OnboardingWorkflowUseCases.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        int count = 0;
        int idx = 0;
        while ((idx = source.IndexOf("_unitOfWork.ExecuteAsync", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "_unitOfWork.ExecuteAsync".Length;
        }

        Assert.True(count >= 3, $"expected ≥3 UoW ExecuteAsync (create/start/rollback), found {count}.");
    }

    [Fact]
    public void Ac2KnownLimitationsDocumentsSec09()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-09", source, StringComparison.Ordinal);
        Assert.Contains("OnboardingWorkflowUseCases", source, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
