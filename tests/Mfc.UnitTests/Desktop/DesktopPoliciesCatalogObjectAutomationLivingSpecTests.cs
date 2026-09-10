using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-A11Y-POLICY-OBJ-01 / W7-195: Policies catalog/object buttons expose AutomationProperties.Name.</summary>
public sealed class DesktopPoliciesCatalogObjectAutomationLivingSpecTests
{
    [Fact]
    public void Ac1PoliciesCatalogObjectButtonsExposeAutomationPropertiesName()
    {
        string main = ReadSource("src/Mfc.Desktop/MainWindow.axaml");

        AssertNameBeforeCommand(main, "Policies.RefreshCatalogCommand", "Refresh catalog");
        AssertNameBeforeCommand(main, "Policies.UpsertAddressCommand", "Upsert address");
        AssertNameBeforeCommand(main, "Policies.UpsertServiceCommand", "Upsert service");
        AssertNameBeforeCommand(main, "Policies.ReplaceContractsCommand", "Replace contracts");
    }

    [Fact]
    public void Ac2Plan23AndTestingDocLockA11yPolicyObj01()
    {
        string root = FindRepoRoot();
        string plan23 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-23-desktop-policies-catalog-object-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.Contains("DESK-A11Y-POLICY-OBJ-01", plan23, StringComparison.Ordinal);
        Assert.Contains("W7-195 DONE", plan23, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesCatalogObjectAutomationLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-195 Living Spec lock)", limitations, StringComparison.Ordinal);
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
