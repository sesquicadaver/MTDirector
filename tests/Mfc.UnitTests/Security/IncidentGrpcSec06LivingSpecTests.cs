using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Incident;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Mfc.Controller.Jobs;
using Mfc.UnitTests.Application.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using ProtoPacketPath = Mfc.Contracts.Mfc.V1.ObservedPacketPathClass;
using ProtoSeverity = Mfc.Contracts.Mfc.V1.IncidentSeverity;
using ProtoSignal = Mfc.Contracts.Mfc.V1.IncidentSignal;
using ProtoSourceType = Mfc.Contracts.Mfc.V1.IncidentSignalSourceType;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-06 (#380) — Incident assessment gRPC surface.</summary>
public sealed class IncidentGrpcSec06LivingSpecTests
{
    private static readonly DateTimeOffset T10 = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T10_01 = new(2026, 8, 22, 10, 0, 1, TimeSpan.Zero);

    [Fact]
    public void Ac1IncidentProtoAndMapGrpcServiceAreRegistered()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "src", "Mfc.Contracts", "Protos", "mfc", "v1", "incident.proto")));
        string program = File.ReadAllText(Path.Combine(root, "src", "Mfc.Controller", "Program.cs"));
        Assert.Contains("MapGrpcService<IncidentGrpcService>", program, StringComparison.Ordinal);
        Assert.Contains("service IncidentService", File.ReadAllText(
            Path.Combine(root, "src", "Mfc.Contracts", "Protos", "mfc", "v1", "incident.proto")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac2IngestIncidentSignalSucceedsWithAuthz()
    {
        IncidentGrpcService service = CreateService(out _);
        ProtoSignal signal = await service.IngestIncidentSignal(
            MinimalIngestRequest(),
            ActorContext("analyst"));

        Assert.Equal("brute_force_login", signal.Category);
        Assert.Equal(85, signal.Confidence);
        Assert.Equal(ProtoSourceType.Siem, signal.SourceType);
        Assert.Equal(ProtoSeverity.High, signal.Severity);
    }

    [Fact]
    public async Task Ac3IngestFailsClosedWithoutPermission()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentSignalIngest);
        IncidentGrpcService service = CreateService(out _, auth);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.IngestIncidentSignal(MinimalIngestRequest(), ActorContext("analyst")));
        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task Ac4BindAssessmentSucceedsAndAuthzFailsClosed()
    {
        FakeAuthorizationBoundary auth = new();
        IncidentGrpcService service = CreateService(out _, auth);

        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        IngestIncidentSignalRequest signal = MinimalIngestRequest(eventId);
        signal.Flow = new IncidentFlowTuple
        {
            SourceAddress = "10.0.0.8",
            DestinationAddress = "198.51.100.10",
            Protocol = "tcp",
        };

        IncidentResponseAssessmentBinding binding = await service.BindIncidentResponseAssessment(
            new BindIncidentResponseAssessmentRequest
            {
                Signal = signal,
                EndpointId = ProtoUuid.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                PresenceId = ProtoUuid.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                EnforcementNodeId = ProtoUuid.FromGuid(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                AssessedAt = Timestamp.FromDateTimeOffset(T10),
                SessionVisibility = IncidentSessionVisibilityStatus.Full,
                PacketPathClass = ProtoPacketPath.CpuFirewall,
            },
            ActorContext("analyst"));

        Assert.Equal(eventId, ProtoUuid.ToGuid(binding.IncidentId));
        Assert.Equal("FullyEnforceable", binding.Assessment.Feasibility);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentAssessmentBind);
        RpcException denied = await Assert.ThrowsAsync<RpcException>(() =>
            service.BindIncidentResponseAssessment(
                new BindIncidentResponseAssessmentRequest
                {
                    Signal = signal,
                    EndpointId = ProtoUuid.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                    PresenceId = ProtoUuid.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                    EnforcementNodeId = ProtoUuid.FromGuid(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    AssessedAt = Timestamp.FromDateTimeOffset(T10),
                    PacketPathClass = ProtoPacketPath.CpuFirewall,
                },
                ActorContext("analyst")));
        Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
    }

    private static IngestIncidentSignalRequest MinimalIngestRequest(Guid? eventId = null) =>
        new()
        {
            EventId = ProtoUuid.FromGuid(eventId ?? Guid.NewGuid()),
            SourceEventId = "siem-evt-42",
            OccurredAt = Timestamp.FromDateTimeOffset(T10),
            ReceivedAt = Timestamp.FromDateTimeOffset(T10_01),
            SourceType = ProtoSourceType.Siem,
            Category = "brute_force_login",
            Severity = ProtoSeverity.High,
            Confidence = 85,
            DeduplicationKey = "dedup:siem:42",
        };

    private static IncidentGrpcService CreateService(
        out FakeAuthorizationBoundary auth,
        FakeAuthorizationBoundary? existing = null)
    {
        auth = existing ?? new FakeAuthorizationBoundary();
        return new IncidentGrpcService(
            new IngestIncidentSignalUseCase(auth),
            new BindIncidentResponseAssessmentUseCase(auth),
            new GrpcRequestActorResolver(Options.Create(new OperationalJobsOptions
            {
                SystemActor = "system:operational-jobs",
            })),
            new TestHostEnvironment(Environments.Development));
    }

    private static TestServerCallContext ActorContext(string actor) =>
        new(new Metadata { { IncidentGrpcService.ActorMetadataKey, actor } });

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = "/tmp";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestServerCallContext(Metadata requestHeaders) : ServerCallContext
    {
        protected override string MethodCore => "test";

        protected override string HostCore => "localhost";

        protected override string PeerCore => "peer";

        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);

        protected override Metadata RequestHeadersCore => requestHeaders;

        protected override CancellationToken CancellationTokenCore => CancellationToken.None;

        protected override Metadata ResponseTrailersCore { get; } = [];

        protected override Status StatusCore { get; set; }

        protected override WriteOptions? WriteOptionsCore { get; set; }

        protected override AuthContext AuthContextCore { get; } = new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }
}
