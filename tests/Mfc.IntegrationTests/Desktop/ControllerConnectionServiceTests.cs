using Mfc.Controller;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Xunit;

namespace Mfc.IntegrationTests.Desktop;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class ControllerConnectionServiceTests
{
    private readonly PostgresFixture _postgres;

    public ControllerConnectionServiceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task ConnectAsyncAgainstHealthyControllerReachesConnected()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var host = Program.BuildHost(
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

        await host.Services.MigrateAsync();
        await host.StartAsync();
        try
        {
            DesktopOptions options = new()
            {
                ControllerEndpoint = url,
                HealthCheckTimeoutSeconds = 5,
                MaxReconnectAttempts = 1,
                ReconnectDelayMilliseconds = 200,
            };

            await using ControllerConnectionService service = new(options);
            await service.ConnectAsync();

            Assert.Equal(ControllerConnectionState.Connected, service.State);
            Assert.Null(service.LastError);
            Assert.NotNull(service.Channel);

            await service.DisconnectAsync();
            Assert.Equal(ControllerConnectionState.Disconnected, service.State);
            Assert.Null(service.Channel);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task ConnectAsyncWhenEndpointUnreachableEndsDisconnected()
    {
        DesktopOptions options = new()
        {
            ControllerEndpoint = $"http://127.0.0.1:{GetFreeTcpPort()}",
            HealthCheckTimeoutSeconds = 1,
            MaxReconnectAttempts = 0,
            ReconnectDelayMilliseconds = 100,
        };

        await using ControllerConnectionService service = new(options);
        await service.ConnectAsync();

        Assert.Equal(ControllerConnectionState.Disconnected, service.State);
        Assert.False(string.IsNullOrWhiteSpace(service.LastError));
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
