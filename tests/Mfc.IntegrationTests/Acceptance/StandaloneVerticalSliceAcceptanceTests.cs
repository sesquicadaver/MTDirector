using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using DomainTrust = Mfc.Domain.Inventory.CertificateTrustMode;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.Acceptance;

/// <summary>
/// M1-30 standalone read-only vertical slice acceptance (in-process Controller + Postgres).
/// Live CHR connectivity is gated separately in <c>Mfc.RouterOs.IntegrationTests</c>.
/// </summary>
[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class StandaloneVerticalSliceAcceptanceTests
{
    private static readonly string[] ExpectedSections =
    [
        "system.identity",
        "firewall.ipv4.filter",
        "network.interfaces",
        "capabilities.device",
    ];

    private readonly PostgresFixture _postgres;

    public StandaloneVerticalSliceAcceptanceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task StandaloneVerticalSliceHashesDiffPersistAndApiSslTrust()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        StandaloneVerticalSliceCapturePort capture = new();

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<ISnapshotCapturePort>();
                builder.Services.AddSingleton<ISnapshotCapturePort>(capture);
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        Uuid firstCaptureId = null!;
        Uuid secondCaptureId = null!;
        string baselineConfigHash = string.Empty;
        string baselineObservationHash = string.Empty;
        string baselineSnapshotHash = string.Empty;
        Guid deviceId = Guid.Empty;

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            SnapshotService.SnapshotServiceClient snapshots = new(channel);
            Metadata headers = ActorHeaders("acceptance");

            Device device = await SeedStandaloneDeviceAsync(inventory, headers);
            deviceId = ProtoUuid.ToGuid(device.Id);

            // AC#2: Controller connection profile uses API-SSL trust (INTERNAL_CA + lab CA profile).
            capture.FilterAction = "accept";
            capture.InterfaceRunning = "true";
            StartCaptureResponse first = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.False(first.Deduplicated);
            Assert.NotNull(first.CaptureId);
            firstCaptureId = first.CaptureId;
            Assert.NotNull(capture.LastTarget);
            Assert.Equal(DomainTrust.InternalCa, capture.LastTarget!.TrustMode);
            Assert.Equal("lab-ca", capture.LastTarget.CaProfileRef);
            Assert.Equal(8729, capture.LastTarget.Endpoint.Port);

            SnapshotSummary baseline = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = first.CaptureId },
                headers,
                deadline: Deadline());
            Assert.Equal(SnapshotCaptureStatus.Completed, baseline.Status);
            Assert.Equal(1u, baseline.SchemaVersion);
            Assert.Equal(32, baseline.ConfigurationHash.Value.Length);
            Assert.Equal(32, baseline.ObservationHash.Value.Length);
            Assert.Equal(32, baseline.CapabilityHash.Value.Length);
            baselineConfigHash = Convert.ToHexString(baseline.ConfigurationHash.Value.Span);
            baselineObservationHash = Convert.ToHexString(baseline.ObservationHash.Value.Span);
            baselineSnapshotHash = Convert.ToHexString(baseline.SnapshotHash.Value.Span);

            // AC#3: supported sections present with capture status.
            foreach (string sectionId in ExpectedSections)
            {
                Assert.Contains(
                    baseline.Sections,
                    s => s.SectionId == sectionId && s.Status == SnapshotSectionCaptureStatus.Ok);
            }

            // AC#4: second capture without changes → identical hashes (deduplicated by snapshot hash).
            StartCaptureResponse identical = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.True(identical.Deduplicated);
            Assert.Equal(first.CaptureId, identical.CaptureId);
            SnapshotSummary identicalSummary = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = identical.CaptureId },
                headers,
                deadline: Deadline());
            Assert.Equal(baselineConfigHash, Convert.ToHexString(identicalSummary.ConfigurationHash.Value.Span));
            Assert.Equal(baselineObservationHash, Convert.ToHexString(identicalSummary.ObservationHash.Value.Span));
            Assert.Equal(baselineSnapshotHash, Convert.ToHexString(identicalSummary.SnapshotHash.Value.Span));

            // AC#5/#6: controlled filter change → configuration hash changes; semantic diff shows MODIFIED.
            capture.FilterAction = "drop";
            StartCaptureResponse filterChanged = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.False(filterChanged.Deduplicated);
            Assert.NotNull(filterChanged.CaptureId);
            secondCaptureId = filterChanged.CaptureId;
            SnapshotSummary afterFilter = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = filterChanged.CaptureId },
                headers,
                deadline: Deadline());
            Assert.NotEqual(baselineConfigHash, Convert.ToHexString(afterFilter.ConfigurationHash.Value.Span));
            Assert.Equal(baselineObservationHash, Convert.ToHexString(afterFilter.ObservationHash.Value.Span));

            DiffPage filterDiff = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = first.CaptureId,
                    RightCaptureId = filterChanged.CaptureId,
                    Page = new PageRequest { PageSize = 100 },
                },
                headers,
                deadline: Deadline());
            Assert.False(filterDiff.Identical);
            Assert.Contains(
                filterDiff.Entries,
                e => e.SectionId == "firewall.ipv4.filter"
                     && e.Domain == DiffDomain.Configuration
                     && e.Changes.Contains(DiffChange.Modified));
            Assert.Contains(
                filterDiff.Entries.SelectMany(e => e.FieldDiffs),
                f => f.FieldName == "action"
                     && f.Before?.StringValue == "accept"
                     && f.After?.StringValue == "drop");

            // AC#7: interface running change → observation hash only (config unchanged).
            capture.InterfaceRunning = "false";
            StartCaptureResponse obsChanged = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            SnapshotSummary afterObs = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = obsChanged.CaptureId },
                headers,
                deadline: Deadline());
            Assert.Equal(
                Convert.ToHexString(afterFilter.ConfigurationHash.Value.Span),
                Convert.ToHexString(afterObs.ConfigurationHash.Value.Span));
            Assert.NotEqual(
                Convert.ToHexString(afterFilter.ObservationHash.Value.Span),
                Convert.ToHexString(afterObs.ObservationHash.Value.Span));

            DiffPage obsDiff = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = filterChanged.CaptureId,
                    RightCaptureId = obsChanged.CaptureId,
                    Page = new PageRequest { PageSize = 100 },
                },
                headers,
                deadline: Deadline());
            Assert.Contains(
                obsDiff.Entries,
                e => e.SectionId == "network.interfaces"
                     && e.Domain == DiffDomain.Observation
                     && (e.Changes.Contains(DiffChange.StateChanged)
                         || e.Changes.Contains(DiffChange.Modified)));
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stop.Token);
        }

        // AC#8: snapshot survives Controller restart (same PostgreSQL).
        string url2 = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app2 = Program.BuildHost(DevArgs(url2, connectionString));
        await app2.StartAsync();
        try
        {
            await WaitForPortAsync(url2, TimeSpan.FromSeconds(10));
            using GrpcChannel channel2 = GrpcChannel.ForAddress(url2);
            SnapshotService.SnapshotServiceClient snapshots2 = new(channel2);
            Metadata headers2 = ActorHeaders("acceptance");
            SnapshotSummary reloaded = await snapshots2.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = firstCaptureId },
                headers2,
                deadline: Deadline());
            Assert.Equal(SnapshotCaptureStatus.Completed, reloaded.Status);
            Assert.Equal(baselineSnapshotHash, Convert.ToHexString(reloaded.SnapshotHash.Value.Span));
            Assert.Equal(deviceId, ProtoUuid.ToGuid(reloaded.DeviceId));

            ListCapturesResponse listed = await snapshots2.ListCapturesAsync(
                new ListCapturesRequest
                {
                    DeviceId = ProtoUuid.FromGuid(deviceId),
                    Page = new PageRequest { PageSize = 50 },
                },
                headers2,
                deadline: Deadline());
            Assert.Contains(listed.Captures, c => c.CaptureId.Equals(secondCaptureId));
        }
        finally
        {
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
            await app2.StopAsync(stop.Token);
        }
    }

    private static async Task<Device> SeedStandaloneDeviceAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers)
    {
        Site site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "CHR30",
                Name = "Standalone CHR Lab",
            },
            headers,
            deadline: Deadline());
        Node node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "standalone",
                DeclaredKind = ProtoNodeKind.Router,
                DeclaredUplinkMode = ProtoDeclaredUplinkMode.One,
            },
            headers,
            deadline: Deadline());
        Device device = await inventory.RegisterDeviceAsync(
            new RegisterDeviceRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                NodeId = node.Id,
                DisplayName = "chr-standalone",
                ManagementHost = "10.255.10.10",
                ManagementPort = 8729,
                Role = ProtoDeviceRole.Router,
            },
            headers,
            deadline: Deadline());
        await inventory.UpdateDeviceConnectionAsync(
            new UpdateDeviceConnectionRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                DeviceId = device.Id,
                Username = "readonly",
                PasswordUtf8 = ByteString.CopyFrom(Encoding.UTF8.GetBytes("ephemeral-lab-secret")),
                TrustMode = ProtoTrust.InternalCa,
                CaProfileRef = "lab-ca",
                ConnectTimeoutMs = 5000,
                CommandTimeoutMs = 30_000,
                MaxResponseBytes = 1_048_576,
            },
            headers,
            deadline: Deadline());
        return device;
    }

    private static Metadata ActorHeaders(string actor) => new()
    {
        { SnapshotGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(45);

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
