using Grpc.Health.V1;
using Grpc.Net.Client;
using Mfc.Controller;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Xunit;

namespace Mfc.IntegrationTests.Controller;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class ControllerHealthHostTests
{
    private readonly PostgresFixture _postgres;

    public ControllerHealthHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task HealthCheckReturnsServingWithoutSecretsOrStackTraces()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(
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

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));

            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            Health.HealthClient client = new(channel);
            HealthCheckResponse response = await client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5));

            Assert.Equal(HealthCheckResponse.Types.ServingStatus.Serving, response.Status);
            string payload = response.ToString();
            Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Exception", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("at Mfc.", payload, StringComparison.Ordinal);
            Assert.DoesNotContain(connectionString, payload, StringComparison.Ordinal);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public void BuildHostRejectsProductionWithoutTls()
    {
        Assert.ThrowsAny<Exception>(() =>
        {
            _ = Program.BuildHost(
                [
                    "--environment", "Production",
                    "--Mfc:Grpc:ListenAddress=http://127.0.0.1:5101",
                    "--Mfc:Grpc:AllowInsecureLoopback=false",
                    "--Mfc:Security:RequireTls=true",
                    "--Mfc:Security:MasterKeyProvider=OsKeyStore",
                    "--Mfc:Database:ConnectionString=Host=127.0.0.1;Database=mfc;Username=mfc;Password=x",
                ]);
        });
    }

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
