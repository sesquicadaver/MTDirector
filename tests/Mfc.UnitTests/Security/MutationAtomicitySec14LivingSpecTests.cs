using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Endpoint;
using Mfc.Application.Models;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Endpoint;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-14 (#396) — UoW for OpenEndpointPresence multi-store writes.</summary>
public sealed class MutationAtomicitySec14LivingSpecTests
{
    [Fact]
    public async Task Ac1OpenPresencePersistsAssessmentAndMigrationInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeEndpointPresenceStore presence = new();
        FakeResponseAssessmentStore assessments = new();
        FakeRoutingAssuranceStateStore routing = new();
        FakeClock clock = new();
        SpyUnitOfWork unitOfWork = new();

        EndpointId endpointId = EndpointId.New();
        SiteId site = SiteId.New();
        NodeId node = NodeId.New();
        OpenEndpointPresenceUseCase useCase = new(auth, presence, assessments, routing, clock, unitOfWork);

        ApplicationResult<EndpointPresenceUpsertResultView> result = await useCase.ExecuteAsync(
            EndpointPresenceTestKit.Command(endpointId, site, node));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.NotNull(await presence.GetActiveIntervalAsync(endpointId));
    }

    [Fact]
    public void Ac2SourceUsesUnitOfWorkForAssessmentAndMigrationWrites()
    {
        string path = Path.Combine(
            FindRepoRoot(), "src", "Mfc.Application", "Endpoint", "EndpointPresenceUseCases.cs");
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        Assert.Contains("_unitOfWork.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("_assessments.SaveAsync", source, StringComparison.Ordinal);
        Assert.Contains("_presence.SaveMigrationAsync", source, StringComparison.Ordinal);

        int uowIdx = source.IndexOf("_unitOfWork.ExecuteAsync", StringComparison.Ordinal);
        int saveIdx = source.IndexOf("_assessments.SaveAsync", StringComparison.Ordinal);
        int migrateIdx = source.IndexOf("_presence.SaveMigrationAsync", StringComparison.Ordinal);
        Assert.True(uowIdx >= 0 && saveIdx > uowIdx && migrateIdx > uowIdx);
    }

    [Fact]
    public void Ac3KnownLimitationsDocumentsSec14()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-14", source, StringComparison.Ordinal);
        Assert.Contains("OpenEndpointPresence", source, StringComparison.Ordinal);
        Assert.Contains("SEC-07", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentWorkflow", source, StringComparison.Ordinal);
        Assert.Contains("UpdateConnectionProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "`OpenEndpointPresenceUseCase` multi-store writes (candidate SEC-14)",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int ExecuteCount { get; private set; }

        public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return action(cancellationToken);
        }
    }
}
