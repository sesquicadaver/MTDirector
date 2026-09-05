using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC AuditService client bound to the current controller channel.</summary>
public sealed class GrpcAuditServiceClient : IAuditServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcAuditServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
        uint pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        AuditService.AuditServiceClient client = CreateClient();
        ListAuditEventsResponse response = await client.ListAuditEventsAsync(
                new ListAuditEventsRequest { PageSize = pageSize },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Events.ToArray();
    }

    private AuditService.AuditServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new AuditService.AuditServiceClient(channel);
    }

    private Metadata ActorHeaders() => DesktopGrpcActorResolver.CreateHeaders(_options);
}
