using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-252 / DESK-CONN-HEALTH-01: Connected-state periodic Health.Check leaves Connected when Controller stops.
/// </summary>
public sealed class DeskConnHealth01W7252LivingSpecTests
{
    [Fact]
    public void Ac1ConnectedBranchProbesHealthAndLeavesConnectedOnFailure()
    {
        string root = RepoRoot();
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Configuration/DesktopOptions.cs"));
        string appsettings = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/appsettings.json"));
        string shell = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/ShellViewModel.cs"));
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string integration = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Desktop/ControllerConnectionServiceTests.cs"));

        Assert.Contains("ProbeConnectedHealthOrLeaveAsync", connection, StringComparison.Ordinal);
        Assert.Contains("ConnectedHealthProbeIntervalMilliseconds", connection, StringComparison.Ordinal);
        Assert.Contains("Health.Check", connection, StringComparison.Ordinal);
        Assert.Contains("HealthCheckTimeoutSeconds", connection, StringComparison.Ordinal);
        Assert.Contains("SetState(ControllerConnectionState.Disconnected", connection, StringComparison.Ordinal);
        Assert.Contains("DisposeChannelAsync", connection, StringComparison.Ordinal);
        Assert.Contains(
            "AuthenticationFailed or ControllerConnectionState.TlsError",
            connection,
            StringComparison.Ordinal);

        Assert.Contains("ConnectedHealthProbeIntervalMilliseconds", options, StringComparison.Ordinal);
        Assert.Contains("ConnectedHealthProbeIntervalMilliseconds", appsettings, StringComparison.Ordinal);

        Assert.Contains("ErrorText = _connection.LastError", shell, StringComparison.Ordinal);
        Assert.Contains("DesktopConnectionStatusText.Format", shell, StringComparison.Ordinal);

        Assert.Contains("ConnectedHealthProbeLeavesConnectedWhenControllerStops", integration, StringComparison.Ordinal);

        Assert.Contains("DESK-CONN-HEALTH-01", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-252", plan29, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-252 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-252 | [#910](https://github.com/sesquicadaver/MTDirector/issues/910) | DESK-CONN-HEALTH-01 — Connected-state periodic gRPC health probe after Controller stop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-392 (#1191)", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-252 DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-392 (#1191)", continuous, StringComparison.Ordinal);
        Assert.Contains("DeskConnHealth01W7252", testing, StringComparison.Ordinal);
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
