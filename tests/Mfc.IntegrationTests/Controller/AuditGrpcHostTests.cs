using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Audit;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mfc.IntegrationTests.Controller;

/// <summary>CT-AUDIT-01: AuditService GrpcHost contract (ListAuditEvents, read-only).</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class AuditGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public AuditGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task ListAuditEventsAfterAppendIsNewestFirstAndPaged()
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
            AuditService.AuditServiceClient audit = new(channel);
            Metadata headers = ActorHeaders("tester");

            ListAuditEventsResponse empty = await audit.ListAuditEventsAsync(
                new ListAuditEventsRequest(),
                headers,
                deadline: Deadline());
            Assert.Empty(empty.Events);

            await AppendAsync(app.Services, "tester", "audit.host.first", """{"n":1}""");
            await AppendAsync(app.Services, "tester", "audit.host.second", """{"n":2}""");

            ListAuditEventsResponse listed = await audit.ListAuditEventsAsync(
                new ListAuditEventsRequest(),
                headers,
                deadline: Deadline());
            Assert.Equal(2, listed.Events.Count);
            Assert.Equal("audit.host.second", listed.Events[0].Action);
            Assert.Equal("audit.host.first", listed.Events[1].Action);
            Assert.Equal("tester", listed.Events[0].Actor);
            Assert.Contains("\"n\"", listed.Events[0].PayloadJson, StringComparison.Ordinal);
            Assert.Contains("2", listed.Events[0].PayloadJson, StringComparison.Ordinal);
            Assert.NotEqual(Guid.Empty, ProtoUuid.ToGuid(listed.Events[0].Id));
            Assert.NotNull(listed.Events[0].OccurredAt);

            ListAuditEventsResponse page = await audit.ListAuditEventsAsync(
                new ListAuditEventsRequest { PageSize = 1 },
                headers,
                deadline: Deadline());
            Assert.Single(page.Events);
            Assert.Equal("audit.host.second", page.Events[0].Action);
            Assert.Equal(listed.Events[0].Id, page.Events[0].Id);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public void AuditServiceHasNoMutationRpcsOnWire()
    {
        string[] methods = AuditService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["ListAuditEvents"], methods);
    }

    private static async Task AppendAsync(IServiceProvider services, string actor, string action, string payloadJson)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IAuditEventWriter writer = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();
        await writer.AppendAsync(actor, action, payloadJson);
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
            "--Mfc:OperationalJobs:Enabled=false",
            $"--Mfc:Database:ConnectionString={connectionString}",
        ];

    private static Metadata ActorHeaders(string actor) => new()
    {
        { InventoryGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(60);

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
