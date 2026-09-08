using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-CONN-01 / W7-99: Desktop Connect/Disconnect Living Spec depth.</summary>
public sealed class DesktopConnectionLivingSpecTests
{
    public DesktopConnectionLivingSpecTests()
    {
        DesktopGrpcActorResolver.ClearCache();
    }

    [Fact]
    public void Ac1ControllerConnectionServiceContractExposesConnectDisconnectAndState()
    {
        Type contract = typeof(IControllerConnectionService);
        Assert.NotNull(contract.GetMethod(nameof(IControllerConnectionService.ConnectAsync)));
        Assert.NotNull(contract.GetMethod(nameof(IControllerConnectionService.DisconnectAsync)));
        Assert.NotNull(contract.GetProperty(nameof(IControllerConnectionService.State)));
        Assert.NotNull(contract.GetProperty(nameof(IControllerConnectionService.LastError)));
        Assert.NotNull(contract.GetProperty(nameof(IControllerConnectionService.Channel)));
        Assert.NotNull(contract.GetEvent(nameof(IControllerConnectionService.StateChanged)));
        Assert.True(typeof(IControllerConnectionService).IsAssignableFrom(typeof(ControllerConnectionService)));
        string source = ReadSource("src/Mfc.Desktop/Services/ControllerConnectionService.cs");
        Assert.Contains("ConnectAsync", source, StringComparison.Ordinal);
        Assert.Contains("DisconnectAsync", source, StringComparison.Ordinal);
        Assert.Contains("SetState(ControllerConnectionState.Disconnected", source, StringComparison.Ordinal);
        Assert.Contains("SetState(ControllerConnectionState.Connecting", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ShellExposesConnectDisconnectCommandsAndStatusSurface()
    {
        Type shell = typeof(ShellViewModel);
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.ConnectCommand)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.DisconnectCommand)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.StatusText)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.ConnectionState)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.ControllerEndpoint)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.ErrorText)));
        Assert.NotNull(shell.GetProperty(nameof(ShellViewModel.IsBusy)));
    }

    [Fact]
    public void Ac3StatusTextFormatsAllConnectionStatesAndShellSyncsFromService()
    {
        DesktopOptions options = new()
        {
            ControllerEndpoint = "https://127.0.0.1:5001",
            Actor = "desk-conn-lab",
            ClientCertificatePath = "",
        };

        Assert.Equal("Disconnected", DesktopConnectionStatusText.Format(ControllerConnectionState.Disconnected, options));
        Assert.Equal("Connecting", DesktopConnectionStatusText.Format(ControllerConnectionState.Connecting, options));
        Assert.Equal("AuthenticationFailed", DesktopConnectionStatusText.Format(ControllerConnectionState.AuthenticationFailed, options));
        Assert.Equal("TlsError", DesktopConnectionStatusText.Format(ControllerConnectionState.TlsError, options));
        Assert.Equal(
            "Connected · actor: desk-conn-lab",
            DesktopConnectionStatusText.Format(ControllerConnectionState.Connected, options));

        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains("DesktopConnectionStatusText.Format", shell, StringComparison.Ordinal);
        Assert.Contains("await _connection.ConnectAsync()", shell, StringComparison.Ordinal);
        Assert.Contains("await _connection.DisconnectAsync()", shell, StringComparison.Ordinal);
        Assert.Contains("ConnectionState = _connection.State", shell, StringComparison.Ordinal);
        Assert.Contains("ErrorText = _connection.LastError", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4ConnectDisconnectCanExecuteRulesAreFailClosed()
    {
        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains("CanExecute = nameof(CanConnect)", shell, StringComparison.Ordinal);
        Assert.Contains("CanExecute = nameof(CanDisconnect)", shell, StringComparison.Ordinal);
        Assert.Contains(
            "ConnectionState is not (ControllerConnectionState.Connecting or ControllerConnectionState.Connected)",
            shell,
            StringComparison.Ordinal);
        Assert.Contains(
            "ConnectionState is not ControllerConnectionState.Disconnected",
            shell,
            StringComparison.Ordinal);
        Assert.Contains("!IsBusy", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5MainWindowBindsConnectDisconnectAndStatus()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("ConnectCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("DisconnectCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("StatusText", axaml, StringComparison.Ordinal);
        Assert.Contains("ControllerEndpoint", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Connect\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Disconnect\"", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6ConnectionStateEnumAndPriorStatusLivingSpecsRemainPresent()
    {
        ControllerConnectionState[] states = Enum.GetValues<ControllerConnectionState>();
        Assert.Contains(ControllerConnectionState.Disconnected, states);
        Assert.Contains(ControllerConnectionState.Connecting, states);
        Assert.Contains(ControllerConnectionState.Connected, states);
        Assert.Contains(ControllerConnectionState.AuthenticationFailed, states);
        Assert.Contains(ControllerConnectionState.TlsError, states);
        Assert.Equal(5, states.Length);

        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionStatusActorW708LivingSpecTests.cs")));
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionStatusAuthFailedW712LivingSpecTests.cs")));
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        Assert.Contains("DESK-CONN-01", plan09, StringComparison.Ordinal);
        Assert.Contains("IControllerConnectionService", plan09, StringComparison.Ordinal);
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
