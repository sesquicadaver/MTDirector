using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Xunit;

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
