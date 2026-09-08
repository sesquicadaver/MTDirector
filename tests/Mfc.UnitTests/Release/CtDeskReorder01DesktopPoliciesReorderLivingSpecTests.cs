using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-REORDER-01 / W7-110: Desktop Policies Move up/down Living Spec is present; PLAN-10 COMPLETE.</summary>
public sealed class CtDeskReorder01DesktopPoliciesReorderLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesReorderLivingSpecAndPlan10CompleteExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesReorderLivingSpecTests.cs");
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopPoliciesReorderLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ViewModelExposesMoveUpDownAndReorderCommands", body, StringComparison.Ordinal);
        Assert.Contains("Ac3MoveCommandsGuardOnCanMoveSelectedRuleAndCallPanelReorderInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac4ReorderRulesCommandParsesUuidListAndCallsPanelInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsMoveUpDownAndSelectedRule", body, StringComparison.Ordinal);
        Assert.Contains("W7-110 DONE", plan10, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-REORDER-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesReorderLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-110 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-10 COMPLETE", limitations, StringComparison.Ordinal);
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
