using System.Text;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Inventory;
using Mfc.Domain.Inventory;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-08 (#385) — UoW for connection profile + deployment workflow writes.</summary>
public sealed class MutationAtomicitySec08LivingSpecTests
{
    [Fact]
    public async Task Ac1UpdateConnectionProfileRunsUpsertAndIdempotencyInsideOneUnitOfWork()
    {
        FakeAuthorizationBoundary auth = new();
        FakeConnectionProfileService profiles = new();
        FakeIdempotencyStore idempotency = new();
        SpyUnitOfWork unitOfWork = new();
        Guid key = Guid.NewGuid();

        UpdateConnectionProfileUseCase useCase = new(auth, profiles, idempotency, unitOfWork);
        ApplicationResult<ConnectionProfileView> result = await useCase.ExecuteAsync(
            new UpsertConnectionProfileCommand
            {
                Actor = "admin",
                IdempotencyKey = key,
                DeviceId = Guid.NewGuid(),
                Username = "ro",
                PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
                TrustMode = CertificateTrustMode.InternalCa,
                CaProfileRef = "lab-ca",
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unitOfWork.ExecuteCount);
        Assert.Single(profiles.Upserts);
    }

    [Fact]
    public void Ac2DeploymentAndProfileSourcesUseUnitOfWorkBoundary()
    {
        AssertSourceUsesUnitOfWork(
            Path.Combine("Inventory", "UpdateConnectionProfileUseCase.cs"),
            expectedMinOccurrences: 1);
        AssertSourceUsesUnitOfWork(
            Path.Combine("Deployment", "DeploymentWorkflowUseCases.cs"),
            expectedMinOccurrences: 3);
    }

    [Fact]
    public void Ac3KnownLimitationsClearsSec07DeployProfileResidual()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "release", "known-limitations.md");
        string source = File.ReadAllText(path);
        Assert.Contains("SEC-08", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Still outside that boundary (candidate SEC-08): `DeploymentWorkflowUseCases` and `UpdateConnectionProfileUseCase`",
            source,
            StringComparison.Ordinal);
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
