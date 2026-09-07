using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT-DRIFT-01 / W7-61: DriftService GrpcHost contract is present and documented.</summary>
public sealed class CtDrift01DriftGrpcHostLivingSpecTests
{
    [Fact]
    public void Ac1DriftGrpcHostTestsAndPlan04MatrixExist()
    {
        string root = RepoRoot();
        string hostTests = Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/DriftGrpcHostTests.cs");
        string plan04 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-04-contract-tests.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(hostTests), "DriftGrpcHostTests.cs missing.");
        string body = File.ReadAllText(hostTests);
        Assert.Contains("ListAndGetDriftEventsAfterDetect", body, StringComparison.Ordinal);
        Assert.Contains("DriftServiceHasNoMutationRpcsOnWire", body, StringComparison.Ordinal);
        Assert.Contains("W7-61 DONE", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-DRIFT-01", testing, StringComparison.Ordinal);
        Assert.Contains("DriftGrpcHostTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-61 Living Spec lock)", limitations, StringComparison.Ordinal);
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
