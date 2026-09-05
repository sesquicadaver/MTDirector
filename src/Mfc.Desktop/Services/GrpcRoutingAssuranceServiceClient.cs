using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC RoutingAssuranceService client bound to the current controller channel.</summary>
public sealed class GrpcRoutingAssuranceServiceClient : IRoutingAssuranceServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcRoutingAssuranceServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<RoutingAssuranceStateDetail> GetDeviceRoutingAssuranceStateAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        RoutingAssuranceService.RoutingAssuranceServiceClient client = CreateClient();
        return await client.GetDeviceRoutingAssuranceStateAsync(
                new GetDeviceRoutingAssuranceStateRequest
                {
                    DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private RoutingAssuranceService.RoutingAssuranceServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new RoutingAssuranceService.RoutingAssuranceServiceClient(channel);
    }

    private Metadata ActorHeaders() => DesktopGrpcActorResolver.CreateHeaders(_options);
}
