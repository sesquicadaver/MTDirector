using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-SHELL-01 / W7-106: Desktop Shell navigation/hotkeys Living Spec depth.</summary>
public sealed class DesktopShellLivingSpecTests
{
    private static readonly ShellNavigationModule[] ExpectedModules =
    [
        ShellNavigationModule.Inventory,
        ShellNavigationModule.Node,
        ShellNavigationModule.Snapshots,
        ShellNavigationModule.Policies,
        ShellNavigationModule.Operations,
        ShellNavigationModule.Drift,
        ShellNavigationModule.Audit,
    ];

    [Fact]
    public void Ac1ShellExposesSevenModulesInStableOrder()
    {
        Assert.Equal(ExpectedModules, Enum.GetValues<ShellNavigationModule>());
        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains("ShellNavigationModule.Inventory", shell, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationModule.Node", shell, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationModule.Snapshots", shell, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationModule.Policies", shell, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationModule.Operations", shell, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationModule.Drift", shell, StringComparison.Ordinal);
        Assert.Contains("ShellNavigationModule.Audit", shell, StringComparison.Ordinal);
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.Modules)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.SelectedModule)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.SelectModuleCommand)));
    }

    [Fact]
    public void Ac2ShellExposesHotKeysTextDocumentingCtrl1Through7AndF5()
    {
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.HotKeysText)));
        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains("Ctrl+1 Inventory", shell, StringComparison.Ordinal);
        Assert.Contains("Ctrl+2 Node", shell, StringComparison.Ordinal);
        Assert.Contains("Ctrl+3 Snapshots", shell, StringComparison.Ordinal);
        Assert.Contains("Ctrl+4 Policies", shell, StringComparison.Ordinal);
        Assert.Contains("Ctrl+5 Operations", shell, StringComparison.Ordinal);
        Assert.Contains("Ctrl+6 Drift", shell, StringComparison.Ordinal);
        Assert.Contains("Ctrl+7 Audit", shell, StringComparison.Ordinal);
        Assert.Contains("F5 Refresh inventory", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3ShellExposesPerModuleSelectionFlagsAndStatusChrome()
    {
        Type shell = typeof(ShellViewModel);
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsInventorySelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsNodeSelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsSnapshotsSelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsPoliciesSelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsOperationsSelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsDriftSelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsAuditSelected)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.StatusText)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.ErrorText)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.HasError)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.ControllerEndpoint)));
    }

    [Fact]
    public void Ac4SelectModuleCommandSetsSelectedModuleInSource()
    {
        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains("[RelayCommand]", shell, StringComparison.Ordinal);
        Assert.Contains("private void SelectModule(ShellNavigationModule module)", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedModule = module", shell, StringComparison.Ordinal);
        Assert.Contains("SelectedModule = ShellNavigationModule.Inventory", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsModuleListHotKeysAndCtrlKeyBindings()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Window.KeyBindings", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+1\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+2\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+3\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+4\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+5\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+6\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Gesture=\"Ctrl+7\"", axaml, StringComparison.Ordinal);
        Assert.Contains("SelectModuleCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Modules}\"", axaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedModule", axaml, StringComparison.Ordinal);
        Assert.Contains("HotKeysText", axaml, StringComparison.Ordinal);
        Assert.Contains("StatusText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6Plan10MatrixAndPriorMvpWorkflowShellCoverageRemainPresent()
    {
        string root = FindRepoRoot();
        string plan10 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-10-desktop-shell-policies-authoring-depth.md"));
        Assert.Contains("DESK-SHELL-01", plan10, StringComparison.Ordinal);
        Assert.Contains("SelectModuleCommand", plan10, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs")));
        string mvp = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopMvpWorkflowsLivingSpecTests.cs"));
        Assert.Contains("HotKeysText", mvp, StringComparison.Ordinal);
        Assert.Contains("SelectModuleCommand", mvp, StringComparison.Ordinal);
        Assert.Contains("Ctrl+1", mvp, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
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
