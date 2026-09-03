using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-10 (#389) — UoW for incident deny overlay expiry writes.</summary>
public sealed class MutationAtomicitySec10LivingSpecTests
{
    [Fact]
    public void Ac1ExpireOverlayBindingUsesUnitOfWorkBoundary()
    {
        string path = Path.Combine(
            FindRepoRoot(), "src", "Mfc.Application", "Incident", "IncidentDenyOverlayRemovalUseCases.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        Assert.Contains("_unitOfWork.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("SaveBindingAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2KnownLimitationsDocumentsSec10()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-10", source, StringComparison.Ordinal);
        Assert.Contains("ExpireIncidentDenyOverlayBinding", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3NoApplicationIdempotencySaveOutsideUnitOfWork()
    {
        string appRoot = Path.Combine(FindRepoRoot(), "src", "Mfc.Application");
        foreach (string file in Directory.EnumerateFiles(appRoot, "*UseCase*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            if (!source.Contains("_idempotency.SaveAsync", StringComparison.Ordinal)
                && !source.Contains("idempotency.SaveAsync", StringComparison.Ordinal))
            {
                continue;
            }

            bool hasUow = source.Contains("IUnitOfWork", StringComparison.Ordinal)
                || source.Contains("unitOfWork.ExecuteAsync", StringComparison.Ordinal)
                || source.Contains("_unitOfWork.ExecuteAsync", StringComparison.Ordinal);
            Assert.True(
                hasUow,
                $"{Path.GetRelativePath(FindRepoRoot(), file)} persists idempotency without IUnitOfWork.");
        }
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
