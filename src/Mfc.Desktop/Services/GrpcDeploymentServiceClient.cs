using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC DeploymentService client bound to the current controller channel.</summary>
public sealed class GrpcDeploymentServiceClient : IDeploymentServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcDeploymentServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DeploymentPlanSummary> CreatePlanAsync(
        Guid nodeId,
        Sha256 logicalPolicyHash,
        Sha256 analysisBundleHash,
        Sha256 topologyHash,
        IReadOnlyList<DeploymentDevicePlanInput> devices,
        CancellationToken cancellationToken = default)
    {
        DeploymentService.DeploymentServiceClient client = CreateClient();
        CreateDeploymentPlanRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            NodeId = DesktopProtoUuid.FromGuid(nodeId),
            LogicalPolicyHash = logicalPolicyHash,
            AnalysisBundleHash = analysisBundleHash,
            TopologyProjectionHash = topologyHash,
        };
        request.Devices.AddRange(devices);
        return await client.CreatePlanAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeploymentOperationSummary> StartAsync(
        Guid planId,
        Sha256 planHash,
        IReadOnlyList<DeploymentPacketPathPairFact> packetPathPairs,
        CancellationToken cancellationToken = default)
    {
        DeploymentService.DeploymentServiceClient client = CreateClient();
        StartDeploymentRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            PlanId = DesktopProtoUuid.FromGuid(planId),
            PlanHash = planHash,
        };
        request.PacketPathPairs.AddRange(packetPathPairs);
        return await client.StartAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<DeploymentProgress> WatchAsync(
        Guid operationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DeploymentService.DeploymentServiceClient client = CreateClient();
        using AsyncServerStreamingCall<DeploymentProgress> call = client.Watch(
            new WatchDeploymentRequest { OperationId = DesktopProtoUuid.FromGuid(operationId) },
            ActorHeaders(),
            cancellationToken: cancellationToken);
        await foreach (DeploymentProgress progress in call.ResponseStream.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return progress;
        }
    }

    public async Task<DeploymentOperationSummary> RollbackAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        DeploymentService.DeploymentServiceClient client = CreateClient();
        return await client.RollbackAsync(
                new RollbackDeploymentRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeploymentRecoveryStatus> GetRecoveryStatusAsync(
        Guid nodeId,
        Guid? operationId = null,
        CancellationToken cancellationToken = default)
    {
        DeploymentService.DeploymentServiceClient client = CreateClient();
        GetDeploymentRecoveryStatusRequest request = new()
        {
            NodeId = DesktopProtoUuid.FromGuid(nodeId),
        };
        if (operationId is Guid id)
        {
            request.OperationId = DesktopProtoUuid.FromGuid(id);
        }

        return await client.GetRecoveryStatusAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private DeploymentService.DeploymentServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new DeploymentService.DeploymentServiceClient(channel);
    }

    private Metadata ActorHeaders() => DesktopGrpcActorResolver.CreateHeaders(_options);
}
