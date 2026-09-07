using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC IncidentService client bound to the current controller channel.</summary>
public sealed class GrpcIncidentServiceClient : IIncidentServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcIncidentServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IncidentSignal> IngestIncidentSignalAsync(
        IngestIncidentSignalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IncidentService.IncidentServiceClient client = CreateClient();
        return await client.IngestIncidentSignalAsync(
                request,
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IncidentResponseAssessmentBinding> BindIncidentResponseAssessmentAsync(
        BindIncidentResponseAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IncidentService.IncidentServiceClient client = CreateClient();
        return await client.BindIncidentResponseAssessmentAsync(
                request,
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private IncidentService.IncidentServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new IncidentService.IncidentServiceClient(channel);
    }

    private Metadata ActorHeaders() => DesktopGrpcActorResolver.CreateHeaders(_options);
}
