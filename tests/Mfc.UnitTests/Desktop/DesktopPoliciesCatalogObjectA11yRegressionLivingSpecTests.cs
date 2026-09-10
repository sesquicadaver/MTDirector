using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-POLICY-OBJ-02 / W7-197: catalog/object + ack/record + authoring residual + lifecycle + Connect/Disconnect + Incident Names; PLAN-23 COMPLETE.</summary>
public sealed class DesktopPoliciesCatalogObjectA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1CatalogObjectAckRecordAuthoringResidualLifecycleAndShellIncidentActionNamesMatrixLocked()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.DoesNotContain("Watermark=", main, StringComparison.Ordinal);

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
    public void Ac2Plan23CompleteAndDocsLockA11yPolicyObj02()
    {
        string root = FindRepoRoot();
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("PLAN-23 COMPLETE", plan23, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-OBJ-02", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-197 DONE", plan23, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesCatalogObjectA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-197 Living Spec lock)", limitations, StringComparison.Ordinal);
    }

    private static void AssertNameBeforeCommand(string main, string commandBinding, string name)
    {
        string needle = $"Command=\"{{Binding {commandBinding}}}\"";
        int index = main.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(index > 0, $"Missing {needle}");
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
