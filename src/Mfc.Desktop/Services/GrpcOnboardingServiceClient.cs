using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC OnboardingService client bound to the current controller channel.</summary>
public sealed class GrpcOnboardingServiceClient : IOnboardingServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcOnboardingServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<OnboardingPrerequisiteReport> ValidatePrerequisitesAsync(
        Guid nodeId,
        IReadOnlyList<OnboardingDevicePrerequisiteFacts> devices,
        CancellationToken cancellationToken = default)
    {
        OnboardingService.OnboardingServiceClient client = CreateClient();
        ValidateOnboardingPrerequisitesRequest request = new()
        {
            NodeId = DesktopProtoUuid.FromGuid(nodeId),
        };
        request.Devices.AddRange(devices);
        return await client.ValidatePrerequisitesAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OnboardingPlanSummary> CreatePlanAsync(
        Guid nodeId,
        Sha256 membershipHash,
        Sha256 topologyHash,
        IReadOnlyList<OnboardingDevicePlanInput> devices,
        CancellationToken cancellationToken = default)
    {
        OnboardingService.OnboardingServiceClient client = CreateClient();
        CreateOnboardingPlanRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            NodeId = DesktopProtoUuid.FromGuid(nodeId),
            NodeMembershipHash = membershipHash,
            TopologyProjectionHash = topologyHash,
        };
        request.Devices.AddRange(devices);
        return await client.CreatePlanAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OnboardingOperationSummary> StartAsync(
        Guid planId,
        Sha256 planHash,
        CancellationToken cancellationToken = default)
    {
        OnboardingService.OnboardingServiceClient client = CreateClient();
        return await client.StartAsync(
                new StartOnboardingRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    PlanId = DesktopProtoUuid.FromGuid(planId),
                    PlanHash = planHash,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<OnboardingProgress> WatchAsync(
        Guid operationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        OnboardingService.OnboardingServiceClient client = CreateClient();
        using AsyncServerStreamingCall<OnboardingProgress> call = client.Watch(
            new WatchOnboardingRequest { OperationId = DesktopProtoUuid.FromGuid(operationId) },
            ActorHeaders(),
            cancellationToken: cancellationToken);
        await foreach (OnboardingProgress progress in call.ResponseStream.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return progress;
        }
    }

    public async Task<OnboardingOperationSummary> RollbackAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        OnboardingService.OnboardingServiceClient client = CreateClient();
        return await client.RollbackAsync(
                new RollbackOnboardingRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OnboardingRecoveryStatus> GetRecoveryStatusAsync(
        Guid nodeId,
        Guid? operationId = null,
        CancellationToken cancellationToken = default)
    {
        OnboardingService.OnboardingServiceClient client = CreateClient();
        GetOnboardingRecoveryStatusRequest request = new()
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

    private OnboardingService.OnboardingServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new OnboardingService.OnboardingServiceClient(channel);
    }

    private Metadata ActorHeaders()
        => new() { { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor } };
}
