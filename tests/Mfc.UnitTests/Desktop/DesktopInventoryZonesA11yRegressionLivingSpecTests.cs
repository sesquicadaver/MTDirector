using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-INV-02 / W7-209: inv/zones Names + ops + Policies/shell/Incident Names; PLAN-25 COMPLETE.</summary>
public sealed class DesktopInventoryZonesA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1InventoryZonesOpsPoliciesShellIncidentActionNamesMatrixLocked()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);

        // DESK-A11Y-INV-01
        AssertNameBeforeNthCommand(main, "Inventory.RefreshCommand", 1, "Refresh");
        AssertNameBeforeNthCommand(main, "AddRouter.ProbeCommand", 0, "Inventory Probe");
        AssertNameBeforeNthCommand(main, "AddRouter.ProbeCommand", 1, "Add router Probe");
        AssertNameBeforeCommand(main, "Zones.RefreshCommand", "Refresh");
        AssertNameBeforeCommand(main, "Zones.ResolveCommand", "Resolve node");
        AssertNameBeforeCommand(main, "Zones.ResolveDeviceCommand", "Resolve device");
        AssertNameBeforeCommand(main, "Zones.CreateZoneCommand", "Create");
        AssertNameBeforeCommand(main, "Zones.DeleteZoneCommand", "Delete");
        AssertNameBeforeCommand(main, "Zones.UpdateZoneCommand", "Update zone");
        AssertNameBeforeCommand(main, "Zones.UpsertBindingCommand", "Upsert binding");
        AssertNameBeforeCommand(main, "Zones.DeleteBindingCommand", "Delete binding");

        // DESK-A11Y-OPS-02 / PLAN-16…24 matrix
        AssertNameBeforeCommand(main, "Onboarding.ValidateCommand", "Validate prerequisites");
        AssertNameBeforeCommand(main, "Onboarding.CreatePlanCommand", "Create plan");
        AssertNameBeforeCommand(main, "Onboarding.StartCommand", "Start");
        AssertNameBeforeCommand(main, "Onboarding.RollbackCommand", "Rollback");
        AssertNameBeforeCommand(main, "Onboarding.RecoveryCommand", "Recovery status");

        AssertNameBeforeCommand(main, "Deployment.CreatePlanCommand", "Create plan");
        AssertNameBeforeCommand(main, "Deployment.StartCommand", "Start");
        AssertNameBeforeCommand(main, "Deployment.RollbackCommand", "Rollback");
        AssertNameBeforeCommand(main, "Deployment.RecoveryCommand", "Recovery status");

        AssertNameBeforeCommand(main, "Policies.RefreshCatalogCommand", "Refresh catalog");
        AssertNameBeforeCommand(main, "Policies.UpsertAddressCommand", "Upsert address");
        AssertNameBeforeCommand(main, "Policies.UpsertServiceCommand", "Upsert service");
        AssertNameBeforeCommand(main, "Policies.ReplaceContractsCommand", "Replace contracts");

        AssertNameBeforeCommand(main, "Policies.RecordAnalysisCommand", "Record analysis");
        AssertNameBeforeCommand(main, "Policies.AcknowledgeWarningCommand", "Acknowledge warning");

        AssertNameBeforeCommand(main, "Policies.LoadCommand", "Load");
        AssertNameBeforeCommand(main, "Policies.CreateDraftCommand", "Create draft");
        AssertNameBeforeCommand(main, "Policies.RefreshSafetyAnalysisCommand", "Analyze safety");
        AssertNameBeforeCommand(main, "Policies.AddRuleCommand", "Add rule");
        AssertNameBeforeCommand(main, "Policies.UpdateRuleCommand", "Update rule");
        AssertNameBeforeCommand(main, "Policies.DeleteRuleCommand", "Delete rule");
        AssertNameBeforeCommand(main, "Policies.MoveRuleUpCommand", "Move up");
        AssertNameBeforeCommand(main, "Policies.MoveRuleDownCommand", "Move down");
        AssertNameBeforeCommand(main, "Policies.ComposeCommand", "Compose findings");
        AssertNameBeforeCommand(main, "Policies.DiffCommand", "Semantic diff");
        AssertNameBeforeCommand(main, "Policies.CompileCommand", "Compile artifacts");

        AssertNameBeforeCommand(main, "Policies.ValidateCommand", "Validate");
        AssertNameBeforeCommand(main, "Policies.SubmitCommand", "Submit for review");
        AssertNameBeforeCommand(main, "Policies.ApproveCommand", "Approve");
        AssertNameBeforeCommand(main, "Policies.BindCommand", "Bind");
        AssertNameBeforeCommand(main, "Policies.DeployCommand", "Deploy");

        AssertNameBeforeCommand(main, "ConnectCommand", "Connect");
        AssertNameBeforeCommand(main, "DisconnectCommand", "Disconnect");

        int ingest = main.IndexOf("Incident.IngestCommand", StringComparison.Ordinal);
        Assert.True(ingest > 0);
        string ingestBefore = main.Substring(Math.Max(0, ingest - 280), Math.Min(280, ingest));
        Assert.Contains("AutomationProperties.Name=\"Ingest signal\"", ingestBefore, StringComparison.Ordinal);

        int bind = main.IndexOf("Incident.BindAssessmentCommand", StringComparison.Ordinal);
        Assert.True(bind > 0);
        string bindBefore = main.Substring(Math.Max(0, bind - 280), Math.Min(280, bind));
        Assert.Contains("AutomationProperties.Name=\"Bind assessment\"", bindBefore, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2Plan25CompleteAndDocsLockA11yInv02()
    {
        string root = FindRepoRoot();
        string plan25 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-25-desktop-inventory-zones-add-router-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-25 COMPLETE", plan25, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-INV-02", plan25, StringComparison.Ordinal);
        Assert.Contains("W7-209 DONE", plan25, StringComparison.Ordinal);
        Assert.Contains("DesktopInventoryZonesA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-209 Living Spec lock)", limitations, StringComparison.Ordinal);
    }

    private static void AssertNameBeforeCommand(string main, string commandBinding, string name)
    {
        AssertNameBeforeNthCommand(main, commandBinding, 0, name);
    }

    private static void AssertNameBeforeNthCommand(string main, string commandBinding, int occurrence, string name)
    {
        string needle = $"Command=\"{{Binding {commandBinding}}}\"";
        int index = -1;
        int start = 0;
        for (int i = 0; i <= occurrence; i++)
        {
            index = main.IndexOf(needle, start, StringComparison.Ordinal);
            Assert.True(index > 0, $"Missing occurrence {i} of {needle}");
            start = index + needle.Length;
        }

        string before = main.Substring(Math.Max(0, index - 280), Math.Min(280, index));
        Assert.Contains($"AutomationProperties.Name=\"{name}\"", before, StringComparison.Ordinal);
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
