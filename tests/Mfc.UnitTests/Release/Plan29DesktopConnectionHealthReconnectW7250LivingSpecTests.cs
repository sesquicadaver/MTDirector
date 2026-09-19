using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-250: PLAN-29 inventory documents ranked DESK-CONN-HEALTH/RECONNECT rows and seeds DESK-CONN-HEALTH-01.</summary>
public sealed class Plan29DesktopConnectionHealthReconnectW7250LivingSpecTests
{
    [Fact]
    public void Ac1Plan29InventoryDocumentsRankedRowsAndSeedsDeskConnHealth01()
    {
        string root = RepoRoot();
        string plan29 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-29-desktop-connection-health-reconnect.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Configuration/DesktopOptions.cs"));

        Assert.Contains("PLAN-29 — Desktop connection health / reconnect after Controller stop", plan29, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan29, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-HEALTH-01", plan29, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-RECONNECT-01", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-252", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-253", plan29, StringComparison.Ordinal);
        Assert.Contains("W7-251", plan29, StringComparison.Ordinal);
        Assert.Contains("RunReconnectLoopAsync", plan29, StringComparison.Ordinal);
        Assert.Contains("ProbeConnectedHealthOrLeaveAsync", plan29, StringComparison.Ordinal);
        Assert.Contains("Health.Check", plan29, StringComparison.Ordinal);
        Assert.Contains("ConnectedHealthProbeIntervalMilliseconds", plan29, StringComparison.Ordinal);
        Assert.Contains("ControllerConnectionService.cs", plan29, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", plan29, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-250 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-HEALTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-252", limitations, StringComparison.Ordinal);

        Assert.Contains("DESK-CONN-HEALTH-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-252", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-250 | [#907](https://github.com/sesquicadaver/MTDirector/issues/907) | PLAN-29 — Inventory Desktop connection health / reconnect after Controller stop (AUDIT §18 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-252 | [#910](https://github.com/sesquicadaver/MTDirector/issues/910) | DESK-CONN-HEALTH-01 — Connected-state periodic gRPC health probe after Controller stop | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-253 | [#911](https://github.com/sesquicadaver/MTDirector/issues/911) | Seed next PLAN-29 row after DESK-CONN-HEALTH-01 → DESK-CONN-RECONNECT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-254 | [#915](https://github.com/sesquicadaver/MTDirector/issues/915) | DESK-CONN-RECONNECT-01 — Bounded reconnect after health-fail drop + shell StatusText/LastError sync | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-352 (#1111)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-251", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-252", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-29", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-29-desktop-connection-health-reconnect.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-29-desktop-connection-health-reconnect.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan29DesktopConnectionHealthReconnectW7250", testing, StringComparison.Ordinal);

        Assert.Contains("RunReconnectLoopAsync", connection, StringComparison.Ordinal);
        Assert.Contains("ControllerConnectionState.Connected", connection, StringComparison.Ordinal);
        Assert.Contains("MaxReconnectAttempts", options, StringComparison.Ordinal);
        Assert.Contains("ReconnectDelayMilliseconds", options, StringComparison.Ordinal);
        Assert.Contains("HealthCheckTimeoutSeconds", options, StringComparison.Ordinal);
        Assert.Contains("ConnectedHealthProbeIntervalMilliseconds", options, StringComparison.Ordinal);
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
