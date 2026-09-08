using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-AUTH-01 / W7-103: Desktop AuthenticationFailed/TlsError Living Spec depth.</summary>
public sealed class DesktopAuthLivingSpecTests
{
    [Fact]
    public void Ac1ConnectionStateExposesAuthenticationFailedAndTlsError()
    {
        Assert.Equal(3, (int)ControllerConnectionState.AuthenticationFailed);
        Assert.Equal(4, (int)ControllerConnectionState.TlsError);
        ControllerConnectionState[] states = Enum.GetValues<ControllerConnectionState>();
        Assert.Contains(ControllerConnectionState.AuthenticationFailed, states);
        Assert.Contains(ControllerConnectionState.TlsError, states);
    }

    [Fact]
    public void Ac2FormatterLocksAuthenticationFailedAndTlsErrorLabelsWithoutActor()
    {
        DesktopOptions options = new() { Actor = "should-not-appear" };

        Assert.Equal(
            "AuthenticationFailed",
            DesktopConnectionStatusText.Format(ControllerConnectionState.AuthenticationFailed, options));
        Assert.Equal(
            "TlsError",
            DesktopConnectionStatusText.Format(ControllerConnectionState.TlsError, options));
        Assert.DoesNotContain(
            "actor:",
            DesktopConnectionStatusText.Format(ControllerConnectionState.AuthenticationFailed, options),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "should-not-appear",
            DesktopConnectionStatusText.Format(ControllerConnectionState.TlsError, options),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3ControllerConnectionServiceMapsUnauthenticatedAndTlsFailures()
    {
        string source = ReadSource("src/Mfc.Desktop/Services/ControllerConnectionService.cs");
        Assert.Contains("ControllerConnectionState.AuthenticationFailed", source, StringComparison.Ordinal);
        Assert.Contains("ControllerConnectionState.TlsError", source, StringComparison.Ordinal);
        Assert.Contains("StatusCode.Unauthenticated", source, StringComparison.Ordinal);
        Assert.Contains("StatusCode.PermissionDenied", source, StringComparison.Ordinal);
        Assert.Contains("IsTlsFailure", source, StringComparison.Ordinal);
        Assert.Contains(
            "AuthenticationFailed or ControllerConnectionState.TlsError",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4ShellCanReconnectFromAuthFailedOrTlsErrorAndSurfacesLastError()
    {
        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        Assert.Contains(
            "ConnectionState is not (ControllerConnectionState.Connecting or ControllerConnectionState.Connected)",
            shell,
            StringComparison.Ordinal);
        Assert.Contains("ErrorText = _connection.LastError", shell, StringComparison.Ordinal);
        Assert.Contains("DesktopConnectionStatusText.Format", shell, StringComparison.Ordinal);
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.ErrorText)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.ConnectCommand)));
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.StatusText)));
    }

    [Fact]
    public void Ac5MainWindowBindsStatusAndErrorForFailClosedAuthPath()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("StatusText", axaml, StringComparison.Ordinal);
        Assert.Contains("ErrorText", axaml, StringComparison.Ordinal);
        Assert.Contains("HasError", axaml, StringComparison.Ordinal);
        Assert.Contains("ConnectCommand", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6PriorW712LivingSpecAndPlan09MatrixRemainPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionStatusAuthFailedW712LivingSpecTests.cs")));
        string w712 = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionStatusAuthFailedW712LivingSpecTests.cs"));
        Assert.Contains("AuthenticationFailed", w712, StringComparison.Ordinal);
        Assert.Contains("TlsError", w712, StringComparison.Ordinal);
        Assert.Contains("Ac1NonConnectedStatusOmitsActorSuffix", w712, StringComparison.Ordinal);
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        Assert.Contains("DESK-AUTH-01", plan09, StringComparison.Ordinal);
        Assert.Contains("AuthenticationFailed", plan09, StringComparison.Ordinal);
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
