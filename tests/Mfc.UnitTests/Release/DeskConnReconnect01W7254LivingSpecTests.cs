using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-254 / DESK-CONN-RECONNECT-01: bounded reconnect after health-fail drop + shell StatusText/LastError sync.
/// </summary>
public sealed class DeskConnReconnect01W7254LivingSpecTests
{
    [Fact]
    public void Ac1HealthFailDropResetsAttemptsPreservesLastErrorAndAdvancesQueue()
    {
        string root = RepoRoot();
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string shell = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/ShellViewModel.cs"));
        string statusText = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopConnectionStatusText.cs"));
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string integration = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Desktop/ControllerConnectionServiceTests.cs"));

        Assert.Contains("preserveLastError", connection, StringComparison.Ordinal);
        Assert.Contains("preserveLastError: true", connection, StringComparison.Ordinal);
        Assert.Contains("attempts = 0", connection, StringComparison.Ordinal);
        Assert.Contains("MaxReconnectAttempts", connection, StringComparison.Ordinal);
        Assert.Contains("ReconnectDelayMilliseconds", connection, StringComparison.Ordinal);
        Assert.Contains(
            "AuthenticationFailed or ControllerConnectionState.TlsError",
            connection,
            StringComparison.Ordinal);

        Assert.Contains("ErrorText = _connection.LastError", shell, StringComparison.Ordinal);
        Assert.Contains("DesktopConnectionStatusText.Format", shell, StringComparison.Ordinal);
        Assert.Contains("Connecting", statusText, StringComparison.Ordinal);

        Assert.Contains("HealthFailDropEntersBoundedReconnectPreservingLastErrorForShell", integration, StringComparison.Ordinal);

        Assert.Contains("DESK-CONN-RECONNECT-01", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-254", plan29, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-254 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-254 | [#915](https://github.com/sesquicadaver/MTDirector/issues/915) | DESK-CONN-RECONNECT-01 — Bounded reconnect after health-fail drop + shell StatusText/LastError sync | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-254 (#915) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-351 (#1107)", continuous, StringComparison.Ordinal);
        Assert.Contains("DeskConnReconnect01W7254", testing, StringComparison.Ordinal);
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
