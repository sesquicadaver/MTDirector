using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-LIST-01 / W7-264: ItemsSource ListBox hosts expose AutomationProperties.Name.</summary>
public sealed class DesktopListBoxHostAutomationLivingSpecTests
{
    private static readonly (string Binding, string Name)[] Hosts =
    [
        ("Modules", "Modules"),
        ("Zones.Zones", "Zones"),
        ("Zones.Bindings", "Node bindings"),
        ("Zones.ResolveResults", "Resolve results / blockers"),
        ("Node.WorkflowDeviceLines", "Device workflow"),
        ("Node.ZoneSummaryLines", "Zones summary"),
        ("Node.VrrpMembers", "VRRP members"),
        ("Node.VrrpPairFindings", "VRRP pair findings"),
        ("Node.DeviceMembers", "Devices"),
        ("Node.DeviceHashLines", "Device hashes"),
        ("RoutingAssurance.ExpectationLines", "Route expectations"),
        ("RoutingAssurance.FindingLines", "Route findings"),
        ("RoutingAssurance.TraceSummaryLines", "Trace summaries"),
        ("Snapshot.VisibleSections", "Snapshot sections"),
        ("Snapshot.ConfigurationRecords", "Configuration records"),
        ("Snapshot.ObservationRecords", "Observation records"),
        ("Diff.SectionGroups", "Diff section groups"),
        ("Diff.VisibleEntries", "Diff entries"),
        ("Policies.Catalog", "Policy catalog"),
        ("Policies.ManagementPathFindingLines", "ManagementPath findings"),
        ("Policies.FastTrackFindingLines", "FastTrack findings"),
        ("Policies.SafetyWitnessLines", "Witnesses"),
        ("Policies.SafetySystemTestLines", "SYSTEM tests"),
        ("Policies.Rules", "Policy rules"),
        ("Policies.AddressObjects", "Address objects"),
        ("Policies.ServiceObjects", "Service objects"),
        ("Policies.ChainContracts", "Chain contracts"),
        ("Policies.DiffRows", "Revision diff"),
        ("Policies.DiffLines", "Revision diff (summary lines)"),
        ("Policies.Findings", "Compose findings"),
        ("Policies.CompileArtifactLines", "Compile artifacts"),
        ("Onboarding.Findings", "Prerequisite checklist"),
        ("Onboarding.Placements", "Anchor placements"),
        ("Onboarding.ProgressLines", "Onboarding progress"),
        ("Deployment.SemanticDiffRows", "Policy semantic diff"),
        ("Deployment.SemanticDiffLines", "Artifact hash delta"),
        ("Deployment.ArtifactLines", "Artifacts"),
        ("Deployment.OrderLines", "Activation / rollback order"),
        ("Deployment.ProbeAndWatchdogLines", "Probes and watchdog"),
        ("Deployment.ProgressLines", "Deployment progress"),
        ("Drift.Events", "Drift events"),
        ("Drift.SelectedEventFindings", "Drift findings"),
        ("Audit.Events", "Audit events"),
    ];

    [Fact]
    public void Ac1AllItemsSourceListBoxHostsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Equal(43, Hosts.Length);

        foreach ((string binding, string name) in Hosts)
        {
            string attrs = ExtractListBoxHostAttrs(main, $"ItemsSource=\"{{Binding {binding}}}\"");
            Assert.Contains($"AutomationProperties.Name=\"{name}\"", attrs, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2DriftAuditReadOnlyTextBoxesNamedByRo01()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains(
            "AutomationProperties.Name=\"Semantic diff\"",
            ExtractReadOnlyTextBoxAttrs(main, "Drift.SemanticDiffText"),
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"Payload (JSON)\"",
            ExtractReadOnlyTextBoxAttrs(main, "Audit.SelectedEvent.PayloadJson"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3Plan31AndTestingDocLockA11yList01()
    {
        string root = FindRepoRoot();
        string plan31 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-31-desktop-residual-listbox-readonly-a11y.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-LIST-01", plan31, StringComparison.Ordinal);
        Assert.Contains("W7-264", plan31, StringComparison.Ordinal);
        Assert.Contains("DesktopListBoxHostAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-264 Living Spec lock)", limitations, StringComparison.Ordinal);
    }

    private static string ExtractListBoxHostAttrs(string axaml, string itemsSourceFragment)
    {
        int idx = axaml.IndexOf("<ListBox", StringComparison.Ordinal);
        while (idx >= 0)
        {
            int end = axaml.IndexOf('>', idx);
            if (end < 0)
            {
                break;
            }

            string attrs = axaml.Substring(idx, end - idx + 1);
            if (attrs.Contains(itemsSourceFragment, StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<ListBox", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"ListBox host not found for {itemsSourceFragment}");
    }

    private static string ExtractReadOnlyTextBoxAttrs(string axaml, string bindingFragment)
    {
        int idx = axaml.IndexOf("<TextBox", StringComparison.Ordinal);
        while (idx >= 0)
        {
            int end = axaml.IndexOf('>', idx);
            if (end < 0)
            {
                break;
            }

            string attrs = axaml.Substring(idx, end - idx + 1);
            if (attrs.Contains(bindingFragment, StringComparison.Ordinal) && attrs.Contains("IsReadOnly=\"True\"", StringComparison.Ordinal))
            {
                return attrs;
            }

            idx = axaml.IndexOf("<TextBox", idx + 1, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"Read-only TextBox not found for {bindingFragment}");
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

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
