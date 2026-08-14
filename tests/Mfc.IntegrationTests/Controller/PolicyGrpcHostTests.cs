using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainPolicy = Mfc.Domain.Policy;

namespace Mfc.IntegrationTests.Controller;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class PolicyGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public PolicyGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task C2CreateDraftAddListUpdateDeleteRulesWithContentHashCas()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient client = new(channel);
            Metadata headers = ActorHeaders("tester");

            PolicyDraft draft = await client.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            Assert.NotNull(draft.RevisionId);
            Assert.Equal(32, draft.ContentHash.Value.Length);

            PolicyRuleMutation added = await client.AddRuleAsync(
                new AddRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    ExpectedContentHash = draft.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Enabled = true,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "allow",
                },
                headers,
                deadline: Deadline());
            Assert.NotNull(added.Rule);
            Assert.Equal(0u, added.Rule.Ordinal);
            Assert.Equal(32, added.ContentHash.Value.Length);

            ListRulesResponse listed = await client.ListRulesAsync(
                new ListRulesRequest { RevisionId = draft.RevisionId },
                headers,
                deadline: Deadline());
            Assert.Single(listed.Rules);
            Assert.Equal(added.ContentHash, listed.ContentHash);

            RpcException cas = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await client.AddRuleAsync(
                    new AddRuleRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        RevisionId = draft.RevisionId,
                        ExpectedContentHash = draft.ContentHash,
                        Family = IpAddressFamily.Ipv4,
                        Chain = PolicyFilterChain.Forward,
                        Stage = PolicyPipelineStage.CompanyAllow,
                        Enabled = true,
                        Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.Aborted, cas.StatusCode);
            Assert.Contains("content_hash", cas.Status.Detail, StringComparison.Ordinal);

            PolicyRuleMutation updated = await client.UpdateRuleAsync(
                new UpdateRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    RuleId = added.Rule.Id,
                    ExpectedContentHash = added.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Enabled = false,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "disabled",
                },
                headers,
                deadline: Deadline());
            Assert.False(updated.Rule!.Enabled);

            PolicyRuleMutation deleted = await client.DeleteRuleAsync(
                new DeleteRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    RuleId = added.Rule.Id,
                    ExpectedContentHash = updated.ContentHash,
                },
                headers,
                deadline: Deadline());
            Assert.Empty(deleted.Rules);

            global::Mfc.Contracts.Mfc.V1.PolicyRevision revision = await client.GetPolicyRevisionAsync(
                new GetPolicyRevisionRequest { RevisionId = draft.RevisionId },
                headers,
                deadline: Deadline());
            Assert.Empty(revision.Rules);
            Assert.Equal(PolicyRevisionState.Draft, revision.State);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task C2ComposeEffectivePolicyReturnsRulesRefsAndHash()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "C2",
                    Name = "Compose C2",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            PolicyDraft draft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            PolicyRuleMutation added = await policy.AddRuleAsync(
                new AddRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    ExpectedContentHash = draft.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Enabled = true,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "allow",
                },
                headers,
                deadline: Deadline());
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(draft.RevisionId));

            EffectivePolicy composed = await policy.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(node.Id, composed.NodeId);
            Assert.Equal(32, composed.LogicalEffectiveHash.Value.Length);
            Assert.NotNull(composed.Company);
            Assert.Equal(draft.PolicyId, composed.Company.PolicyId);
            Assert.Equal(draft.RevisionId, composed.Company.RevisionId);
            Assert.Equal(1u, composed.Company.RevisionNumber);
            Assert.Equal(32, composed.Company.ContentHash.Value.Length);
            Assert.Null(composed.Site);
            Assert.Null(composed.Node);
            Assert.Single(composed.Rules);
            Assert.Equal(added.Rule!.Id, composed.Rules[0].Id);
            Assert.Equal("allow", composed.Rules[0].Description);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task O1TwoActiveCompaniesDecodeComposeTrailer()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "O1",
                    Name = "Compose O1",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-a",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-b",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());

            RpcException ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await policy.ComposeEffectivePolicyAsync(
                    new ComposeEffectivePolicyRequest { NodeId = node.Id },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
            byte[]? trailer = ex.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
            Assert.NotNull(trailer);
            ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
            Assert.StartsWith("POLICY_COMPOSE_", detail.Code, StringComparison.Ordinal);
            Assert.NotEqual("failed", detail.Code, StringComparer.Ordinal);
            Assert.NotEqual("conflict", detail.Code, StringComparer.Ordinal);
            Assert.Equal(DomainPolicy.PolicyComposeCodes.PolicyNotUnique, detail.Code);
            Assert.False(detail.Retryable);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task O3UnusedPolicyObjectFindingOnSuccess()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "O3",
                    Name = "Compose O3",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            PolicyDraft draft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            Guid unused = Guid.NewGuid();
            await ReplaceDraftWithUnusedObjectAsync(
                app,
                ProtoUuid.ToGuid(draft.RevisionId),
                unused);
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(draft.RevisionId));

            EffectivePolicy composed = await policy.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Contains(composed.Findings, f => f.Code == DomainPolicy.PolicyComposeCodes.UnusedPolicyObject);
            Assert.Contains(composed.Findings, f => f.Subject == unused.ToString("D"));
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    private static async Task ApproveRevisionAsync(WebApplication app, Guid revisionId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.PolicyRevision? revision = await store.GetRevisionAsync(new PolicyRevisionId(revisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);
        await store.SaveRevisionAsync(revision);
    }

    private static async Task ReplaceDraftWithUnusedObjectAsync(WebApplication app, Guid revisionId, Guid unusedObjectId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.PolicyRevision? revision = await store.GetRevisionAsync(new PolicyRevisionId(revisionId));
        Assert.NotNull(revision);
        DomainPolicy.PolicyDocument document = new(
            DomainPolicy.PolicyKind.CompanyBaseline,
            DomainPolicy.PolicyOwnerScope.Company,
            addressObjects:
            [
                System.Text.Json.JsonDocument.Parse("{\"id\":\"" + unusedObjectId + "\"}").RootElement.Clone(),
            ],
            rules:
            [
                DomainPolicy.PolicyRule.Create(
                    Mfc.Domain.Inventory.IpAddressFamily.IPv4,
                    DomainPolicy.PolicyFilterChain.Forward,
                    DomainPolicy.PolicyPipelineStage.CompanyAllow,
                    ordinal: 0,
                    DomainPolicy.TrafficPredicate.Create(),
                    DomainPolicy.RuleEffectSpec.Create(DomainPolicy.PolicyRuleEffect.Accept)),
            ]);
        revision!.ReplaceDocument(document, parentContextHash: null);
        await store.SaveRevisionAsync(revision);
    }

    private static string[] DevArgs(string url, string connectionString)
        =>
        [
            "--environment", "Development",
            $"--Mfc:Grpc:ListenAddress={url}",
            "--Mfc:Grpc:AllowInsecureLoopback=true",
            "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
            "--Mfc:Security:RequireTls=true",
            "--Mfc:Security:MasterKeyProvider=Development",
            "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
            $"--Mfc:Database:ConnectionString={connectionString}",
        ];

    private static Metadata ActorHeaders(string actor) => new()
    {
        { PolicyGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(30);

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(string url, TimeSpan timeout)
    {
        Uri uri = new(url);
        using CancellationTokenSource delay = new(timeout);
        while (!delay.IsCancellationRequested)
        {
            try
            {
                using System.Net.Sockets.TcpClient client = new();
                await client.ConnectAsync(uri.Host, uri.Port, delay.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(50, delay.Token);
            }
        }

        throw new TimeoutException($"Timed out waiting for {url}");
    }
}
