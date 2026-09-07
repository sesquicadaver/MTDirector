using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-INCIDENT-03 / W7-75: Desktop Incident Bind assessment Living Spec.</summary>
public sealed class CtDeskIncident03DesktopIncidentBindLivingSpecTests
{
    [Fact]
    public void Ac1DesktopIncidentBindLivingSpecAndPlan06MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopIncidentBindLivingSpecTests.cs");
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopIncidentBindLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac1ViewModelExposesBindAssessmentCommandAndBindingFields", body, StringComparison.Ordinal);
        Assert.Contains("Ac3BindMapsAssessmentBindingFromClientPayload", body, StringComparison.Ordinal);
        Assert.Contains("Ac4MainWindowBindsBindAssessmentFormAndResult", body, StringComparison.Ordinal);
        Assert.Contains("Ac5HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-75 DONE", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-03", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopIncidentBindLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-75 Living Spec lock)", limitations, StringComparison.Ordinal);
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
