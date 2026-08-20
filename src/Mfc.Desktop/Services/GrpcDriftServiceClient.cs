using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC DriftService client bound to the current controller channel.</summary>
public sealed class GrpcDriftServiceClient : IDriftServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcDriftServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<DriftEvent>> ListDeviceDriftEventsAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        DriftService.DriftServiceClient client = CreateClient();
        ListDeviceDriftEventsResponse response = await client.ListDeviceDriftEventsAsync(
                new ListDeviceDriftEventsRequest { DeviceId = DesktopProtoUuid.FromGuid(deviceId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Events.ToArray();
    }

    public async Task<DriftEvent> GetDriftEventAsync(
        Guid driftEventId,
        CancellationToken cancellationToken = default)
    {
        DriftService.DriftServiceClient client = CreateClient();
        return await client.GetDriftEventAsync(
                new GetDriftEventRequest { DriftEventId = DesktopProtoUuid.FromGuid(driftEventId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private DriftService.DriftServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new DriftService.DriftServiceClient(channel);
    }

    private Metadata ActorHeaders()
        => new() { { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor } };
}
