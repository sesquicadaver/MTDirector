using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Controller;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Mfc.IntegrationTests.Persistence;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class PolicyApprovalPersistTests
{
    private readonly PostgresFixture _postgres;

    public PolicyApprovalPersistTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateCreatesApprovalTablesAndSchemaMetadata()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("PolicyApprovalBindingSchemaM217", StringComparison.Ordinal));
        Assert.NotNull(await db.SchemaMetadata.FindAsync(SchemaMetadataEntitySeed.PolicyApprovalSchemaKey));

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY[
                'policy_analysis_runs',
                'policy_approvals',
                'policy_bindings',
                'warning_acknowledgments'])
            ORDER BY table_name;
            """;
        List<string> tables = [];
        {
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(
            ["policy_analysis_runs", "policy_approvals", "policy_bindings", "warning_acknowledgments"],
            tables);

        cmd.CommandText = """
            SELECT indexname
            FROM pg_indexes
            WHERE tablename = 'policy_bindings'
              AND indexname = 'uq_policy_bindings_exception_policy_active';
            """;
        object? index = await cmd.ExecuteScalarAsync();
        Assert.Equal("uq_policy_bindings_exception_policy_active", index);
    }

    [Fact]
    public async Task AnalysisRunAndApprovalAreAppendOnlyBindingStateCanChange()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore policies = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        IPolicyApprovalStore approvals = scope.ServiceProvider.GetRequiredService<IPolicyApprovalStore>();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            ownerId: null);
        PolicyRevision revision = PolicyRevision.CreateDraft(
            policy,
            1,
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope),
            null,
            UserId.New(),
            DateTimeOffset.UtcNow);
        revision.MarkValidated();
        revision.SubmitForReview();
        await policies.AddPolicyAsync(policy);
        await policies.AddRevisionAsync(revision);

        PolicyAnalysisRun run = PolicyAnalysisRun.Create(
            revision.Id,
            revision.ContentHash,
            H("logical"),
            H("analysis"),
            H("evidence"),
            H("topology"),
            H("impact"),
            [H("device")],
            H("deps"),
            PolicyEvidenceAnalysisCodes.RiskLow,
            evidenceSignalsPresent: true,
            PolicyApprovalCodes.AnalyzerVersion,
            PolicyDocument.SchemaName,
            PolicyPipelineV1.Version,
            [],
            [
                new PolicyApprovalTestOutcome
                {
                    TestId = PolicyTestId.New(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
            UserId.New(),
            DateTimeOffset.UtcNow);
        await approvals.AddAnalysisRunAsync(run);

        db.ChangeTracker.Clear();
        PolicyAnalysisRunEntity runEntity = await db.PolicyAnalysisRuns.SingleAsync(r => r.Id == run.Id.Value);
        runEntity.RiskLevel = "HIGH";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PolicyAnalysisRuns.Remove(await db.PolicyAnalysisRuns.SingleAsync(r => r.Id == run.Id.Value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        PolicyApproval vote = PolicyApproval.Create(
            revision.Id, run.Id, run.BundleHash, UserId.New(), false, DateTimeOffset.UtcNow);
        await approvals.AddApprovalAsync(vote);
        db.ChangeTracker.Clear();
        PolicyApprovalEntity voteEntity = await db.PolicyApprovals.SingleAsync(a => a.Id == vote.Id.Value);
        voteEntity.IsSecurityOwner = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        revision.Approve(DateTimeOffset.UtcNow);
        await policies.SaveRevisionAsync(revision);
        PolicyDesiredBinding binding = PolicyDesiredBinding.Activate(
            policy, revision, run, DateTimeOffset.UtcNow, null, null);
        await approvals.AddBindingAsync(binding);
        binding.Disable(DateTimeOffset.UtcNow);
        await approvals.SaveBindingAsync(binding);
        PolicyDesiredBinding? loaded = await approvals.GetBindingAsync(binding.Id);
        Assert.Equal(PolicyBindingState.Disabled, loaded!.State);
        Assert.Equal(2ul, loaded.RowVersion);
    }

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static WebApplication BuildApp(string connectionString)
    {
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        return Program.BuildHost(
            [
                "--environment", "Development",
                $"--Mfc:Grpc:ListenAddress={url}",
                "--Mfc:Grpc:AllowInsecureLoopback=true",
                "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
                "--Mfc:Security:RequireTls=true",
                "--Mfc:Security:MasterKeyProvider=Development",
                "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                "--Mfc:OperationalJobs:Enabled=false",
                $"--Mfc:Database:ConnectionString={connectionString}",
            ]);
    }

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
