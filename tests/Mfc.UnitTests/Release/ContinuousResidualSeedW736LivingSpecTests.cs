using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-36: known-limitations / queue seed locks next continuous residual tranche (W7-37 gRPC automation).</summary>
public sealed class ContinuousResidualSeedW736LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedGrpcAutomationResidualAsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));

        Assert.Contains("Intentional residual (W7-36 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("gRPC remains available for automation", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-37", roadmap, StringComparison.Ordinal);
        Assert.Contains("Lock gRPC remains available for automation residual Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-37", plan, StringComparison.Ordinal);
        Assert.Contains("gRPC remains available for automation", plan, StringComparison.Ordinal);
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
