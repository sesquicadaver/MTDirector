using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mfc.IntegrationTests.Controller;

/// <summary>CT-INCIDENT-01: IncidentService GrpcHost contract (Ingest + BindAssessment, SEC-06 scoped).</summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class IncidentGrpcHostTests
{
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10_01 = new(2026, 8, 22, 10, 0, 1, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;

    public IncidentGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task IngestAndBindAssessmentOverHost()
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
            IncidentService.IncidentServiceClient incident = new(channel);
            Metadata headers = ActorHeaders("tester");

            Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            IngestIncidentSignalRequest ingest = MinimalIngestRequest(eventId);
            IncidentSignal signal = await incident.IngestIncidentSignalAsync(
                ingest,
                headers,
                deadline: Deadline());
            Assert.Equal("brute_force_login", signal.Category);
            Assert.Equal(85, signal.Confidence);
            Assert.Equal(IncidentSignalSourceType.Siem, signal.SourceType);
            Assert.Equal(IncidentSeverity.High, signal.Severity);
            Assert.Equal(eventId, ProtoUuid.ToGuid(signal.EventId));

            ingest.Flow = new IncidentFlowTuple
            {
                SourceAddress = "10.0.0.8",
                DestinationAddress = "198.51.100.10",
                Protocol = "tcp",
            };
            IncidentResponseAssessmentBinding binding = await incident.BindIncidentResponseAssessmentAsync(
                new BindIncidentResponseAssessmentRequest
                {
                    Signal = ingest,
                    EndpointId = ProtoUuid.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                    PresenceId = ProtoUuid.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                    EnforcementNodeId = ProtoUuid.FromGuid(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    AssessedAt = Timestamp.FromDateTimeOffset(T10),
                    SessionVisibility = IncidentSessionVisibilityStatus.Full,
                    PacketPathClass = ObservedPacketPathClass.CpuFirewall,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(eventId, ProtoUuid.ToGuid(binding.IncidentId));
            Assert.Equal("FullyEnforceable", binding.Assessment.Feasibility);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public void IncidentServiceExposesOnlyIngestAndBindOnWire()
    {
        string[] methods = IncidentService.Descriptor.Methods
            .Select(static m => m.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(["BindIncidentResponseAssessment", "IngestIncidentSignal"], methods);
    }

    private static IngestIncidentSignalRequest MinimalIngestRequest(Guid eventId) =>
        new()
        {
            EventId = ProtoUuid.FromGuid(eventId),
            SourceEventId = "siem-evt-42",
            OccurredAt = Timestamp.FromDateTimeOffset(T10),
            ReceivedAt = Timestamp.FromDateTimeOffset(T10_01),
            SourceType = IncidentSignalSourceType.Siem,
            Category = "brute_force_login",
            Severity = IncidentSeverity.High,
            Confidence = 85,
            DeduplicationKey = "dedup:siem:42",
        };

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
