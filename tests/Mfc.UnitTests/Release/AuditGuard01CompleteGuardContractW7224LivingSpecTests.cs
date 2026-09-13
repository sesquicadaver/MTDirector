using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-224: AUDIT-GUARD-01 ManagementPath enforces complete guard contract.</summary>
public sealed class AuditGuard01CompleteGuardContractW7224LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndAnalysisLockAuditGuard01()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string analysis = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Policy/ManagementPathAnalysis.cs"));
        string marker = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Policy/ActualFilterMarker.cs"));

        Assert.Contains("AUDIT-GUARD-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-224", plan26, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUARD-01", limitations, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-224 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-224", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-GUARD-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-224", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditGuard01CompleteGuardContractW7224", testing, StringComparison.Ordinal);
        Assert.Contains("IntersectsManagementWitness", analysis, StringComparison.Ordinal);
        Assert.Contains("IsDefaultRoute", analysis, StringComparison.Ordinal);
        Assert.Contains("TryParseStrictGuardMarker", marker, StringComparison.Ordinal);
        Assert.Contains("MfcGuardV1Prefix", marker, StringComparison.Ordinal);
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
