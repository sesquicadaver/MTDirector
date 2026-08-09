using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Authorization;
using Mfc.Controller.Grpc;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ProtoDeclaredUplinkMode = Mfc.Contracts.Mfc.V1.DeclaredUplinkMode;
using ProtoDeviceRole = Mfc.Contracts.Mfc.V1.DeviceRole;
using ProtoNodeKind = Mfc.Contracts.Mfc.V1.NodeKind;
using ProtoTrust = Mfc.Contracts.Mfc.V1.CertificateTrustMode;

namespace Mfc.IntegrationTests.Controller;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class SnapshotGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public SnapshotGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task StartWatchListSectionCompareAndCancel()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        DeterministicSnapshotCapturePort capture = new();

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<ISnapshotCapturePort>();
                builder.Services.AddSingleton<ISnapshotCapturePort>(capture);
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            SnapshotService.SnapshotServiceClient snapshots = new(channel);
            Metadata headers = ActorHeaders("tester");

            (Device device, _) = await SeedDeviceWithConnectionAsync(inventory, headers, "192.0.2.26");

            Guid idem = Guid.NewGuid();
            StartCaptureResponse started = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(idem),
                },
                headers,
                deadline: Deadline());
            Assert.False(started.Deduplicated);
            Assert.NotNull(started.CaptureId);
            Assert.Equal(16, started.CaptureId.Value.Length);
            Assert.Equal(1, capture.CaptureCount);

            List<CaptureProgress> progress = [];
            using var watchCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await foreach (CaptureProgress item in snapshots.WatchCapture(
                               new WatchCaptureRequest { OperationId = started.OperationId },
                               headers,
                               cancellationToken: watchCts.Token)
                           .ResponseStream.ReadAllAsync(watchCts.Token))
            {
                progress.Add(item);
            }

            Assert.Contains(progress, p => p.Stage == CaptureStage.Completed);
            Assert.Equal(started.CaptureId, progress.Last(p => p.Stage == CaptureStage.Completed).CaptureId);

            StartCaptureResponse replay = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(idem),
                },
                headers,
                deadline: Deadline());
            Assert.True(replay.Deduplicated);
            Assert.Equal(1, capture.CaptureCount);

            ListCapturesResponse listed = await snapshots.ListCapturesAsync(
                new ListCapturesRequest
                {
                    DeviceId = device.Id,
                    Page = new PageRequest { PageSize = 1 },
                },
                headers,
                deadline: Deadline());
            Assert.Single(listed.Captures);
            Assert.Equal(32, listed.Captures[0].SnapshotHash.Value.Length);
            Assert.Equal(32, listed.Captures[0].ConfigurationHash.Value.Length);

            SnapshotSummary summary = await snapshots.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = started.CaptureId },
                headers,
                deadline: Deadline());
            Assert.Equal(32, summary.SnapshotHash.Value.Length);
            Assert.Equal(SnapshotCaptureStatus.Completed, summary.Status);

            SnapshotSectionPage section = await snapshots.GetSnapshotSectionAsync(
                new GetSnapshotSectionRequest
                {
                    CaptureId = started.CaptureId,
                    SectionId = "system.identity",
                    Domain = DiffDomain.Configuration,
                    Page = new PageRequest { PageSize = 1 },
                },
                headers,
                deadline: Deadline());
            Assert.Equal("system.identity", section.SectionId);
            Assert.NotEmpty(section.Records);
            Assert.Equal("lab-router", section.Records[0].Configuration
                .First(f => f.Name == "name").Value.StringValue);
            Assert.False(string.IsNullOrEmpty(section.NextPageToken));

            SnapshotSectionPage sectionPage2 = await snapshots.GetSnapshotSectionAsync(
                new GetSnapshotSectionRequest
                {
                    CaptureId = started.CaptureId,
                    SectionId = "system.identity",
                    Domain = DiffDomain.Configuration,
                    Page = new PageRequest { PageSize = 1, PageToken = section.NextPageToken },
                },
                headers,
                deadline: Deadline());
            Assert.Single(sectionPage2.Records);
            Assert.NotEqual(section.Records[0].StableKey, sectionPage2.Records[0].StableKey);

            DiffPage identical = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = started.CaptureId,
                    RightCaptureId = started.CaptureId,
                    Page = new PageRequest { PageSize = 50 },
                },
                headers,
                deadline: Deadline());
            Assert.True(identical.Identical);
            Assert.Empty(identical.Entries);

            capture.Variant = 2;
            StartCaptureResponse second = await snapshots.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = device.Id,
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                },
                headers,
                deadline: Deadline());
            Assert.NotEqual(started.CaptureId, second.CaptureId);

            DiffPage changed = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = started.CaptureId,
                    RightCaptureId = second.CaptureId,
                    Page = new PageRequest { PageSize = 50 },
                },
                headers,
                deadline: Deadline());
            Assert.False(changed.Identical);
            Assert.NotEmpty(changed.Entries);
            DiffPage changedAgain = await snapshots.CompareSnapshotsAsync(
                new CompareSnapshotsRequest
                {
                    LeftCaptureId = started.CaptureId,
                    RightCaptureId = second.CaptureId,
                    Page = new PageRequest { PageSize = 50 },
                },
                headers,
                deadline: Deadline());
            Assert.Equal(
                changed.Entries.Select(e => e.SectionId + "|" + e.RecordKey + "|" + string.Join(',', e.Changes)).ToArray(),
                changedAgain.Entries.Select(e => e.SectionId + "|" + e.RecordKey + "|" + string.Join(',', e.Changes)).ToArray());

            // Cancel WatchCapture mid-flight without hanging.
            Guid cancelOp = ProtoUuid.ToGuid(
                (await snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        DeviceId = device.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline())).OperationId);
            using var cancelWatch = new CancellationTokenSource();
            AsyncServerStreamingCall<CaptureProgress> stream = snapshots.WatchCapture(
                new WatchCaptureRequest { OperationId = ProtoUuid.FromGuid(cancelOp) },
                headers,
                cancellationToken: cancelWatch.Token);
            cancelWatch.Cancel();
            Exception canceled = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await foreach (CaptureProgress _ in stream.ResponseStream.ReadAllAsync(cancelWatch.Token))
                {
                }
            });
            Assert.True(
                canceled is OperationCanceledException
                || (canceled is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled),
                $"Unexpected cancel exception: {canceled.GetType().Name}");

            // No password/secret fields on wire responses.
            AssertNoSensitiveFieldNames(summary);
            AssertNoSensitiveFieldNames(section);
            AssertNoSensitiveFieldNames(changed);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task NodeCaptureIsRejectedAndAuthIsEnforced()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(
            DevArgs(url, connectionString),
            builder =>
            {
                builder.Services.RemoveAll<ISnapshotCapturePort>();
                builder.Services.AddSingleton<ISnapshotCapturePort>(new DeterministicSnapshotCapturePort());
            });

        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            InventoryService.InventoryServiceClient inventory = new(channel);
            SnapshotService.SnapshotServiceClient snapshots = new(channel);
            Metadata headers = ActorHeaders("tester");

            (Device device, Node node) = await SeedDeviceWithConnectionAsync(inventory, headers, "192.0.2.27");

            RpcException nodeEx = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await snapshots.StartCaptureAsync(
                    new StartCaptureRequest
                    {
                        NodeId = node.Id,
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.FailedPrecondition, nodeEx.StatusCode);
            Assert.Contains("node capture deferred", nodeEx.Status.Detail, StringComparison.OrdinalIgnoreCase);

            string denyUrl = $"http://127.0.0.1:{GetFreeTcpPort()}";
            await using var denyApp = Program.BuildHost(
                DevArgs(denyUrl, await _postgres.CreateFreshDatabaseAsync()),
                builder =>
                {
                    builder.Services.RemoveAll<IAuthorizationBoundary>();
                    builder.Services.AddSingleton<IAuthorizationBoundary, DenyAllAuthorizationBoundary>();
                    builder.Services.RemoveAll<ISnapshotCapturePort>();
                    builder.Services.AddSingleton<ISnapshotCapturePort>(new DeterministicSnapshotCapturePort());
                });
            await denyApp.Services.MigrateAsync();
            await denyApp.StartAsync();
            try
            {
                await WaitForPortAsync(denyUrl, TimeSpan.FromSeconds(10));
                using GrpcChannel denyChannel = GrpcChannel.ForAddress(denyUrl);
                SnapshotService.SnapshotServiceClient denyClient = new(denyChannel);
                RpcException denied = await Assert.ThrowsAsync<RpcException>(async () =>
                {
                    await denyClient.ListCapturesAsync(
                        new ListCapturesRequest
                        {
                            DeviceId = device.Id,
                            Page = new PageRequest { PageSize = 10 },
                        },
                        ActorHeaders("guest"),
                        deadline: Deadline());
                });
                Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
            }
            finally
            {
                using CancellationTokenSource stop = new(TimeSpan.FromSeconds(5));
                await denyApp.StopAsync(stop.Token);
            }
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    private static async Task<(Device Device, Node Node)> SeedDeviceWithConnectionAsync(
        InventoryService.InventoryServiceClient inventory,
        Metadata headers,
        string host)
    {
        Site site = await inventory.CreateSiteAsync(
            new CreateSiteRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                Code = "SNAP26",
                Name = "Snapshot Lab",
            },
            headers,
            deadline: Deadline());
        Node node = await inventory.CreateNodeAsync(
            new CreateNodeRequest
            {
                IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                SiteId = site.Id,
                Name = "edge",
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
                DisplayName = "edge",
                ManagementHost = host,
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
                PasswordUtf8 = ByteString.CopyFrom(Encoding.UTF8.GetBytes("super-secret-password")),
                TrustMode = ProtoTrust.InternalCa,
                CaProfileRef = "lab-ca",
                ConnectTimeoutMs = 5000,
                CommandTimeoutMs = 30_000,
                MaxResponseBytes = 1_048_576,
            },
            headers,
            deadline: Deadline());
        return (device, node);
    }

    private static void AssertNoSensitiveFieldNames(Google.Protobuf.IMessage message)
    {
        string text = message.ToString() ?? string.Empty;
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ciphertext", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret", text, StringComparison.OrdinalIgnoreCase);
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
        { SnapshotGrpcService.ActorMetadataKey, actor },
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

    /// <summary>Public test double producing parseable canonical section payloads.</summary>
    public sealed class DeterministicSnapshotCapturePort : ISnapshotCapturePort
    {
        private int _captureCount;

        public int CaptureCount => Volatile.Read(ref _captureCount);

        public int Variant { get; set; } = 1;

        public Task<SnapshotCaptureResult> CaptureAsync(
            RouterOsReadTarget target,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _captureCount);
            cancellationToken.ThrowIfCancellationRequested();

            string name = Variant == 1 ? "lab-router" : "lab-router-v2";
            string note = Variant == 1 ? "alpha" : "beta";
            CanonicalSection section = Canonicalizer.Canonicalize(new CanonicalSectionInput
            {
                Domain = CanonicalDomain.Configuration,
                SectionId = CanonicalSectionIds.SystemIdentity,
                Ordered = false,
                Records =
                [
                    new CanonicalRecordInput
                    {
                        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["name"] = name,
                            ["note"] = note,
                        },
                    },
                    new CanonicalRecordInput
                    {
                        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["name"] = name + "-b",
                            ["note"] = note,
                        },
                    },
                ],
            });

            byte[] digest = new byte[32];
            digest[0] = (byte)Variant;
            digest[1] = 0x26;
            for (int i = 2; i < digest.Length; i++)
            {
                digest[i] = (byte)(i + Variant);
            }

            Hash256 hash = Hash256.Create(digest);
            byte[] body = section.Utf8Bytes;
            return Task.FromResult(new SnapshotCaptureResult
            {
                ConfigurationHash = ConfigurationHash.FromDigest(hash),
                ObservationHash = ObservationHash.FromDigest(hash),
                CapabilityHash = CapabilityHash.FromDigest(hash),
                SnapshotHash = SnapshotHash.FromDigest(hash),
                SchemaVersion = 1,
                RawPayload = Encoding.UTF8.GetBytes("{\"sanitized\":true}"),
                ConfigurationPayload = body,
                ObservationPayload = body,
                CapabilityPayload = body,
                Sections =
                [
                    new CapturedSectionDescriptor
                    {
                        SectionId = section.SectionId,
                        SectionVersion = 1,
                        Status = 1,
                        Ordered = section.Ordered,
                        ConfigurationRecordCount = section.Records.Count,
                        ConfigurationPayload = body,
                    },
                ],
            });
        }
    }
}
