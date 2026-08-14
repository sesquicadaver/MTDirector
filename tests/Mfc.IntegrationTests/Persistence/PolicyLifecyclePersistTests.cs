using Mfc.Application.Abstractions.Persistence;
using Mfc.Controller;
using Mfc.Domain.Inventory;
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
public sealed class PolicyLifecyclePersistTests
{
    private readonly PostgresFixture _postgres;

    public PolicyLifecyclePersistTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task MigrateCreatesPoliciesTablesAndSchemaMetadata()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();

        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("PolicyLifecycleSchema", StringComparison.Ordinal));

        SchemaMetadataEntity? meta = await db.SchemaMetadata.FindAsync(
            SchemaMetadataEntitySeed.PolicyLifecycleSchemaKey);
        Assert.NotNull(meta);
        Assert.Equal(SchemaMetadataEntitySeed.PolicyLifecycleSchemaValue, meta.Value);

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(ARRAY['policies', 'policy_revisions'])
            ORDER BY table_name;
            """;
        List<string> tables = [];
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(["policies", "policy_revisions"], tables);
    }

    [Fact]
    public async Task PersistDraftRoundTripsBrotliPayloadHashMatchesUncompressed()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();

        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("company-baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            ownerId: null);
        PolicyRevision draft = PolicyRevision.CreateDraft(
            policy,
            1,
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope),
            parentContextHash: null,
            UserId.New(),
            DateTimeOffset.UtcNow);

        await store.AddPolicyAsync(policy);
        await store.AddRevisionAsync(draft);

        PolicyRevision? loaded = await store.GetRevisionAsync(draft.Id);
        Assert.NotNull(loaded);
        Assert.Equal(draft.ContentHash.ToString(), loaded.ContentHash.ToString());
        Assert.Equal(draft.CanonicalBytes, loaded.CanonicalBytes);
        Assert.Equal(PolicyRevisionState.Draft, loaded.State);
        Assert.Equal(1u, await store.GetLatestRevisionNumberAsync(policy.Id));
    }

    [Fact]
    public async Task DraftEditPersistsNewHashAndInvalidatesValidationState()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();

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
        await store.AddPolicyAsync(policy);
        await store.AddRevisionAsync(revision);

        revision.MarkValidated();
        await store.SaveRevisionAsync(revision);
        Hash256 before = revision.ContentHash;

        revision.ReplaceDocument(
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope).WithRules(
            [
                PolicyRule.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    PolicyPipelineStage.CompanyDeny,
                    ordinal: 0,
                    TrafficPredicate.Create(),
                    RuleEffectSpec.Create(PolicyRuleEffect.Drop),
                    id: new RuleId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"))),
            ]),
            null);
        await store.SaveRevisionAsync(revision);

        PolicyRevision? loaded = await store.GetRevisionAsync(revision.Id);
        Assert.NotNull(loaded);
        Assert.Equal(PolicyRevisionState.Draft, loaded.State);
        Assert.NotEqual(before.ToString(), loaded.ContentHash.ToString());
    }

    [Fact]
    public async Task ApprovedPayloadIsImmutableDeleteForbiddenLifecycleStateAllowed()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        MfcDbContext db = scope.ServiceProvider.GetRequiredService<MfcDbContext>();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();

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
        revision.Approve(DateTimeOffset.UtcNow);
        await store.AddPolicyAsync(policy);
        await store.AddRevisionAsync(revision);

        db.ChangeTracker.Clear();
        PolicyRevisionEntity entity = await db.PolicyRevisions.SingleAsync(r => r.Id == revision.Id.Value);
        byte[] originalPayload = entity.CompressedPayload.ToArray();
        entity.CompressedPayload = [1, 2, 3];
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        PolicyRevisionEntity toDelete = await db.PolicyRevisions.SingleAsync(r => r.Id == revision.Id.Value);
        db.PolicyRevisions.Remove(toDelete);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        revision.Supersede();
        await store.SaveRevisionAsync(revision);
        PolicyRevision? loaded = await store.GetRevisionAsync(revision.Id);
        Assert.NotNull(loaded);
        Assert.Equal(PolicyRevisionState.Superseded, loaded.State);
        Assert.Equal(originalPayload, (await db.PolicyRevisions.AsNoTracking().SingleAsync(r => r.Id == revision.Id.Value)).CompressedPayload);
    }

    [Fact]
    public async Task CloneApprovedPersistsNewDraftRevision()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();

        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            ownerId: null);
        PolicyRevision approved = PolicyRevision.CreateDraft(
            policy,
            1,
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope),
            null,
            UserId.New(),
            DateTimeOffset.UtcNow);
        approved.MarkValidated();
        approved.SubmitForReview();
        approved.Approve(DateTimeOffset.UtcNow);
        await store.AddPolicyAsync(policy);
        await store.AddRevisionAsync(approved);

        PolicyRevision clone = approved.CloneToDraft(policy, 2, UserId.New(), DateTimeOffset.UtcNow);
        await store.AddRevisionAsync(clone);

        IReadOnlyList<PolicyRevision> list = await store.ListRevisionsAsync(policy.Id);
        Assert.Equal(2, list.Count);
        Assert.Equal(PolicyRevisionState.Approved, list[0].State);
        Assert.Equal(PolicyRevisionState.Draft, list[1].State);
        Assert.Equal(list[0].ContentHash.ToString(), list[1].ContentHash.ToString());
    }

    [Fact]
    public async Task SiteOverlayPersistsParentContextHash()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        await using WebApplication app = BuildApp(connectionString);
        await app.Services.MigrateAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();

        Hash256 companyHash = PolicyHashing.HashContent(
            PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company));
        Hash256 parent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.SiteOverlay, companyHash, null, null, null)!;

        Mfc.Domain.Policy.Policy overlay = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("site-overlay"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            Guid.NewGuid());
        PolicyRevision draft = PolicyRevision.CreateDraft(
            overlay,
            1,
            PolicyDocument.CreateEmpty(overlay.Kind, overlay.OwnerScope),
            parent,
            UserId.New(),
            DateTimeOffset.UtcNow);
        await store.AddPolicyAsync(overlay);
        await store.AddRevisionAsync(draft);

        PolicyRevision? loaded = await store.GetRevisionAsync(draft.Id);
        Assert.NotNull(loaded);
        Assert.Equal(parent.ToString(), loaded.ParentContextHash!.ToString());
    }

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
