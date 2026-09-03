using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Zones;
using Mfc.Domain.Policy;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-07 (#383) — extend atomic mutation boundary beyond SEC-05 inventory.</summary>
public sealed class MutationAtomicitySec07LivingSpecTests
{
    [Fact]
    public async Task Ac1CreateZoneRunsMutationIdempotencyAndAuditInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeZoneDefinitionStore zones = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        SpyUnitOfWork unitOfWork = new();
        Guid key = Guid.NewGuid();

        CreateZoneDefinitionUseCase useCase = new(auth, zones, idempotency, audit, unitOfWork);
        ApplicationResult<ZoneDefinitionView> result = await useCase.ExecuteAsync(
            new CreateZoneDefinitionCommand
            {
                Actor = "admin",
                IdempotencyKey = key,
                OwnerScope = PolicyOwnerScope.Company,
                Key = "sec07",
                Name = "Sec07 Zone",
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.Contains(audit.Events, e => e.Action == CreateZoneDefinitionUseCase.Operation);

        ApplicationResult<ZoneDefinitionView> replay = await useCase.ExecuteAsync(
            new CreateZoneDefinitionCommand
            {
                Actor = "admin",
                IdempotencyKey = key,
                OwnerScope = PolicyOwnerScope.Company,
                Key = "sec07",
                Name = "Sec07 Zone",
            });
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(result.Value!.Id, replay.Value!.Id);
        Assert.Equal(1, unitOfWork.ExecuteCount);
    }

    [Fact]
    public void Ac2ZoneAndPolicyMutationSourcesUseUnitOfWorkBoundary()
    {
        AssertSourceUsesUnitOfWork(
            Path.Combine("Zones", "ZoneDefinitionUseCases.cs"),
            expectedMinOccurrences: 3);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Zones", "NodeZoneBindingUseCases.cs"),
            expectedMinOccurrences: 2);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Policies", "PolicyRuleUseCases.cs"),
            expectedMinOccurrences: 1);
        string policyRules = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "Mfc.Application", "Policies", "PolicyRuleUseCases.cs"));
        Assert.Contains("unitOfWork.ExecuteAsync", policyRules, StringComparison.Ordinal);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Policies", "PolicyApprovalUseCases.cs"),
            expectedMinOccurrences: 5);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Policies", "ValidateRevisionUseCase.cs"),
            expectedMinOccurrences: 1);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Policies", "UpdateExceptionMetadataUseCase.cs"),
            expectedMinOccurrences: 1);
        string catalogPath = Path.Combine(FindRepoRoot(), "src", "Mfc.Application", "Policies", "PolicyCatalogUseCases.cs");
        string catalog = File.ReadAllText(catalogPath);
        Assert.Contains("IUnitOfWork unitOfWork", catalog, StringComparison.Ordinal);
        Assert.Contains("unitOfWork.ExecuteAsync", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac3DocumentedResidualOutsideSec07()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-07", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentWorkflow", source, StringComparison.Ordinal);
        Assert.Contains("UpdateConnectionProfile", source, StringComparison.Ordinal);
    }

    private static void AssertSourceUsesUnitOfWork(string relativeUnderApplication, int expectedMinOccurrences)
    {
        string path = Path.Combine(FindRepoRoot(), "src", "Mfc.Application", relativeUnderApplication);
        string source = File.ReadAllText(path);
        Assert.Contains("IUnitOfWork unitOfWork", source, StringComparison.Ordinal);
        int count = 0;
        int idx = 0;
        while ((idx = source.IndexOf("_unitOfWork.ExecuteAsync", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += "_unitOfWork.ExecuteAsync".Length;
        }

        Assert.True(
            count >= expectedMinOccurrences,
            $"{relativeUnderApplication}: expected ≥{expectedMinOccurrences} UoW ExecuteAsync, found {count}.");
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
