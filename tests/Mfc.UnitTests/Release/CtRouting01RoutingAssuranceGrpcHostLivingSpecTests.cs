using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT-ROUTING-01 / W7-63: RoutingAssurance ProtoContract + GrpcHost are present and documented.</summary>
public sealed class CtRouting01RoutingAssuranceGrpcHostLivingSpecTests
{
    [Fact]
    public void Ac1RoutingAssuranceProtoAndGrpcHostTestsAndPlan04MatrixExist()
    {
        string root = RepoRoot();
        string hostTests = Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/RoutingAssuranceGrpcHostTests.cs");
        string protoTests = Path.Combine(root, "tests/Mfc.UnitTests/Contracts/RoutingAssuranceProtoContractTests.cs");
        string plan04 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-04-contract-tests.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(hostTests), "RoutingAssuranceGrpcHostTests.cs missing.");
        Assert.True(File.Exists(protoTests), "RoutingAssuranceProtoContractTests.cs missing.");
        string body = File.ReadAllText(hostTests);
        Assert.Contains("GetDeviceRoutingAssuranceStateAfterUpsert", body, StringComparison.Ordinal);
        Assert.Contains("RoutingAssuranceServiceHasNoMutationRpcsOnWire", body, StringComparison.Ordinal);
        Assert.Contains("W7-63 DONE", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-ROUTING-01", testing, StringComparison.Ordinal);
        Assert.Contains("RoutingAssuranceGrpcHostTests", testing, StringComparison.Ordinal);
        Assert.Contains("RoutingAssuranceProtoContractTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-63 Living Spec lock)", limitations, StringComparison.Ordinal);
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
