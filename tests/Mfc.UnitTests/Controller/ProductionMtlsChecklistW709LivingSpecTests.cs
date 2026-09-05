using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-09: Production mTLS operator checklist in pilot runbook.</summary>
public sealed class ProductionMtlsChecklistW709LivingSpecTests
{
    [Fact]
    public void Ac1PilotRunbookDocumentsProductionMtlsChecklist()
    {
        string path = Path.Combine(RepoRoot(), "docs/operations/pilot-runbook.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Production mTLS checklist (W7-09)", content, StringComparison.Ordinal);
        Assert.Contains("ClientCertificateMode=RequireCertificate", content, StringComparison.Ordinal);
        Assert.Contains("ClientCaProfileRef", content, StringComparison.Ordinal);
        Assert.Contains("ClientCertificatePath", content, StringComparison.Ordinal);
        Assert.Contains("Connected · actor:", content, StringComparison.Ordinal);
        Assert.Contains("AllowMetadataActor=false", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac1ControllerConfigurationDocumentsMtlsKeys()
    {
        string path = Path.Combine(RepoRoot(), "docs/operations/controller-configuration.md");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);

        Assert.Contains("Grpc:ClientCertificateMode", content, StringComparison.Ordinal);
        Assert.Contains("TrustedCa:ClientCaProfileRef", content, StringComparison.Ordinal);
        Assert.Contains("Desktop:ClientCertificatePath", content, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln"))
                || File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
