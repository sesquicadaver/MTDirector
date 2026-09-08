using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-SHELL-01 / W7-106: Desktop Shell Living Spec is present and documented.</summary>
public sealed class CtDeskShell01DesktopShellLivingSpecTests
{
    [Fact]
    public void Ac1DesktopShellLivingSpecAndPlan10MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopShellLivingSpecTests.cs");
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopShellLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac2ShellExposesHotKeysTextDocumentingCtrl1Through7AndF5", body, StringComparison.Ordinal);
        Assert.Contains("Ac3ShellExposesPerModuleSelectionFlagsAndStatusChrome", body, StringComparison.Ordinal);
        Assert.Contains("Ac4SelectModuleCommandSetsSelectedModuleInSource", body, StringComparison.Ordinal);
        Assert.Contains("Ac5MainWindowBindsModuleListHotKeysAndCtrlKeyBindings", body, StringComparison.Ordinal);
        Assert.Contains("W7-106 DONE", plan10, StringComparison.Ordinal);
        Assert.Contains("DESK-SHELL-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopShellLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-106 Living Spec lock)", limitations, StringComparison.Ordinal);
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
