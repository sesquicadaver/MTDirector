using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT-INCIDENT-01 / W7-64: Incident ProtoContract + GrpcHost are present and documented.</summary>
public sealed class CtIncident01IncidentGrpcHostLivingSpecTests
{
    [Fact]
    public void Ac1IncidentProtoAndGrpcHostTestsAndPlan04MatrixExist()
    {
        string root = RepoRoot();
        string hostTests = Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/IncidentGrpcHostTests.cs");
        string protoTests = Path.Combine(root, "tests/Mfc.UnitTests/Contracts/IncidentProtoContractTests.cs");
        string plan04 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-04-contract-tests.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(hostTests), "IncidentGrpcHostTests.cs missing.");
        Assert.True(File.Exists(protoTests), "IncidentProtoContractTests.cs missing.");
        string body = File.ReadAllText(hostTests);
        Assert.Contains("IngestAndBindAssessmentOverHost", body, StringComparison.Ordinal);
        Assert.Contains("IncidentServiceExposesOnlyIngestAndBindOnWire", body, StringComparison.Ordinal);
        Assert.Contains("W7-64 DONE", plan04, StringComparison.Ordinal);
        Assert.Contains("CT-INCIDENT-01", testing, StringComparison.Ordinal);
        Assert.Contains("IncidentGrpcHostTests", testing, StringComparison.Ordinal);
        Assert.Contains("IncidentProtoContractTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-64 Living Spec lock)", limitations, StringComparison.Ordinal);
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
