using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-360: PLAN-54 inventory documents sole DESK-CONN-FAULT-01 rank
/// (2 AuthenticationFailed sites copy Status.Detail; connection service does not call DesktopRpcFaultText)
/// and opens the implement after seed.
/// </summary>
public sealed class Plan54DesktopConnectionStatusFaultTextW7360LivingSpecTests
{
    [Fact]
    public void Ac1Plan54InventoryDocumentsSoleDeskConnFault01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan54 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-54-desktop-connection-status-fault-text.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string connection = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/ControllerConnectionService.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string shell = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/ShellViewModel.cs"));

        Assert.Contains("PLAN-54 — Desktop connection-status fault text after Controller fault-correlation logging", plan54, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan54, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", plan54, StringComparison.Ordinal);
        Assert.Contains("e51073f6", plan54, StringComparison.Ordinal);
        Assert.Contains("470696e9", plan54, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan54, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status.Detail", plan54, StringComparison.Ordinal);
        Assert.Contains("W7-362", plan54, StringComparison.Ordinal);
        Assert.Contains("W7-363", plan54, StringComparison.Ordinal);
        Assert.Contains("W7-361", plan54, StringComparison.Ordinal);
        Assert.Contains("W7-360", plan54, StringComparison.Ordinal);
        Assert.Contains("ShellViewModel", plan54, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-363 (#1131)", plan54, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-360 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-CONN-FAULT-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-362", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-361", limitations, StringComparison.Ordinal);
        Assert.Contains("e51073f6", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-360 | [#1127](https://github.com/sesquicadaver/MTDirector/issues/1127) | PLAN-54 — Inventory Desktop connection-status fault text | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-361 | [#1128](https://github.com/sesquicadaver/MTDirector/issues/1128) | Seed first PLAN-54 atomic row after inventory → DESK-CONN-FAULT-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-362 | [#1130](https://github.com/sesquicadaver/MTDirector/issues/1130) | DESK-CONN-FAULT-01 — Route connection AuthenticationFailed status through DesktopRpcFaultText | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-363 | [#1131](https://github.com/sesquicadaver/MTDirector/issues/1131) | Seed next after DESK-CONN-FAULT-01 (PLAN-54 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-363 (#1131)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-361", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-362", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-54", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-54-desktop-connection-status-fault-text.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-54-desktop-connection-status-fault-text.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan54DesktopConnectionStatusFaultTextW7360", testing, StringComparison.Ordinal);

        Assert.Equal(2, Count(connection, "SetState(ControllerConnectionState.AuthenticationFailed, DesktopRpcFaultText.Format(ex))"));
        Assert.DoesNotContain("ex.Status.Detail", connection, StringComparison.Ordinal);
        Assert.Contains("ErrorText = _connection.LastError", shell, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", fault, StringComparison.Ordinal);
        Assert.Contains("correlation", fault, StringComparison.Ordinal);

        int sites = 0;
        string viewModels = Path.Combine(root, "src/Mfc.Desktop/ViewModels");
        foreach (string path in Directory.EnumerateFiles(viewModels, "*ViewModel.cs"))
        {
            sites += Count(File.ReadAllText(path), "DesktopRpcFaultText.Format");
        }

        Assert.Equal(14, sites);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + value.Length;
        }
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
