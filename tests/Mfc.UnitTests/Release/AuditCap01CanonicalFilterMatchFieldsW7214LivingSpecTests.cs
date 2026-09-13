using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-214: AUDIT-CAP-01 canonical filter projects firewall match fields.</summary>
public sealed class AuditCap01CanonicalFilterMatchFieldsW7214LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndProjectorLockAuditCap01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string projector = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs"));

        Assert.Contains("AUDIT-CAP-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-214", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-214 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-214", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-214", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditCap01CanonicalFilterMatchFieldsW7214", testing, StringComparison.Ordinal);
        Assert.Contains("BuildFilterConfigurationProperties", projector, StringComparison.Ordinal);
        Assert.Contains("IsFilterObservationOnlyProperty", projector, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-01", projector, StringComparison.Ordinal);
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
