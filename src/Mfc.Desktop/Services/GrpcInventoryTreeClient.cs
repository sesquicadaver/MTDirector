using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>Paged InventoryService client bound to the current controller channel.</summary>
public sealed class GrpcInventoryTreeClient : IInventoryTreeClient
{
    private const uint DefaultPageSize = 50;

    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcInventoryTreeClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<Site>> ListAllSitesAsync(CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        Metadata headers = ActorHeaders();
        List<Site> all = [];
        string pageToken = string.Empty;
        do
        {
            ListSitesResponse response = await client.ListSitesAsync(
                    new ListSitesRequest
                    {
                        Page = new PageRequest { PageSize = DefaultPageSize, PageToken = pageToken },
                    },
                    headers,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            all.AddRange(response.Sites);
            pageToken = response.Page?.NextPageToken ?? string.Empty;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return all;
    }

    public async Task<IReadOnlyList<Node>> ListAllNodesAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        Metadata headers = ActorHeaders();
        List<Node> all = [];
        string pageToken = string.Empty;
        do
        {
            ListNodesResponse response = await client.ListNodesAsync(
                    new ListNodesRequest
                    {
                        SiteId = DesktopProtoUuid.FromGuid(siteId),
                        Page = new PageRequest { PageSize = DefaultPageSize, PageToken = pageToken },
                    },
                    headers,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            all.AddRange(response.Nodes);
            pageToken = response.Page?.NextPageToken ?? string.Empty;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return all;
    }

    public async Task<NodeDetails> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        return await client.GetNodeAsync(
                new GetNodeRequest { NodeId = DesktopProtoUuid.FromGuid(nodeId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private InventoryService.InventoryServiceClient CreateClient()
    {
        GrpcChannel? channel = _connection.Channel;
        if (channel is null || _connection.State != ControllerConnectionState.Connected)
        {
            throw new InvalidOperationException("Controller channel is not connected.");
        }

        return new InventoryService.InventoryServiceClient(channel);
    }

    private Metadata ActorHeaders() => new()
    {
        { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor.Trim() },
    };
}
