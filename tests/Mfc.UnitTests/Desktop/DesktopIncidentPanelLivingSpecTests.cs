using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>
/// DESK-INCIDENT-02 / W7-74: MainWindow Operations → Incident panel bindings + empty-state Living Spec.
/// </summary>
public sealed class DesktopIncidentPanelLivingSpecTests
{
    [Fact]
    public void Ac1MainWindowBindsIncidentIngestFormStatusAndEmptyState()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("<!-- Incident (SEC-06 ingest; no deploy/overlay Desktop RPCs) -->", axaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Incident\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.IngestCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.SourceEventId", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.Category", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.DeduplicationKey", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.Confidence", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.StatusText", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.ErrorText", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.HasError", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.HasLastSignal", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.LastSignal.SummaryLine", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident.LastSignal.EventIdText", axaml, StringComparison.Ordinal);
        Assert.Contains("Connect to Controller", axaml, StringComparison.Ordinal);
        Assert.Contains("No deploy/overlay/feedback", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Incident.DeployCommand", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpireOverlay", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2PanelStaysUnderOperationsWithoutEighthNavigationModule()
    {
        string[] modules = Enum.GetNames<ShellNavigationModule>();
        Assert.Equal(7, modules.Length);
        Assert.DoesNotContain("Incident", modules, StringComparer.Ordinal);

        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("IsVisible=\"{Binding IsOperationsSelected}\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Incident signal ingest", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellNavigationModule.Incident", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+8", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3EmptyStateAndLastSignalSurfaceExistOnViewModel()
    {
        Type vm = typeof(IncidentViewModel);
        Assert.NotNull(vm.GetProperty(nameof(IncidentViewModel.HasLastSignal)));
        Assert.NotNull(vm.GetProperty(nameof(IncidentViewModel.HasError)));
        Assert.NotNull(vm.GetProperty(nameof(IncidentViewModel.StatusText)));
        Assert.NotNull(vm.GetProperty(nameof(IncidentViewModel.ErrorText)));
        Assert.NotNull(vm.GetProperty(nameof(IncidentViewModel.LastSignal)));
        Assert.NotNull(typeof(IncidentSignalListItem).GetProperty(nameof(IncidentSignalListItem.SummaryLine)));
        Assert.NotNull(typeof(IncidentSignalListItem).GetProperty(nameof(IncidentSignalListItem.EventIdText)));
    }

    [Fact]
    public void Ac4HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs"));
        Assert.Contains("IngestAndBindAssessmentOverHost", host, StringComparison.Ordinal);
        Assert.Contains("IncidentServiceExposesOnlyIngestAndBindOnWire", host, StringComparison.Ordinal);
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
