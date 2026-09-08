using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-DRAFT-01 / W7-122: Desktop Policies Create/Load draft Living Spec is present and documented.</summary>
public sealed class CtDeskDraft01DesktopPoliciesDraftLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesDraftLivingSpecAndPlan12MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesDraftLivingSpecTests.cs");
        string plan12 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-12-desktop-policies-residual-lifecycle.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesDraftLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesCreateDraftLoadCommandsAndSurface", body, StringComparison.Ordinal);
        Assert.Contains("Ac3CreateDraftRequiresNameAndCallsPanelInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4LoadParsesRevisionIdAndCallsPanelInSource", body, StringComparison.Ordinal);
        Assert.Contains("W7-122 DONE", plan12, StringComparison.Ordinal);
        Assert.Contains("DESK-DRAFT-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesDraftLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-122 Living Spec lock)", limitations, StringComparison.Ordinal);
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
