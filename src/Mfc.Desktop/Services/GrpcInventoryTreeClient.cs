using Google.Protobuf;
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

    public async Task<Site> CreateSiteAsync(
        string code,
        string name,
        CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        return await client.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = code,
                    Name = name,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Node> CreateNodeAsync(
        Guid siteId,
        string name,
        NodeKind declaredKind,
        DeclaredUplinkMode declaredUplinkMode,
        CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        return await client.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = DesktopProtoUuid.FromGuid(siteId),
                    Name = name,
                    DeclaredKind = declaredKind,
                    DeclaredUplinkMode = declaredUplinkMode,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Device> RegisterDeviceAsync(
        Guid nodeId,
        string displayName,
        string managementHost,
        uint managementPort,
        DeviceRole role,
        CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        return await client.RegisterDeviceAsync(
                new RegisterDeviceRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    NodeId = DesktopProtoUuid.FromGuid(nodeId),
                    DisplayName = displayName,
                    ManagementHost = managementHost,
                    ManagementPort = managementPort,
                    Role = role,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeviceConnectionSummary> UpdateDeviceConnectionAsync(
        Guid deviceId,
        string username,
        ReadOnlyMemory<byte> passwordUtf8,
        CertificateTrustMode trustMode,
        string? caProfileRef,
        Sha256? pinnedSpkiSha256,
        uint connectTimeoutMs,
        uint commandTimeoutMs,
        ulong maxResponseBytes,
        CancellationToken cancellationToken = default)
    {
        InventoryService.InventoryServiceClient client = CreateClient();
        UpdateDeviceConnectionRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            DeviceId = DesktopProtoUuid.FromGuid(deviceId),
            Username = username,
            PasswordUtf8 = ByteString.CopyFrom(passwordUtf8.Span),
            TrustMode = trustMode,
            ConnectTimeoutMs = connectTimeoutMs,
            CommandTimeoutMs = commandTimeoutMs,
            MaxResponseBytes = maxResponseBytes,
        };
        if (!string.IsNullOrWhiteSpace(caProfileRef))
        {
            request.CaProfileRef = caProfileRef.Trim();
        }

        if (pinnedSpkiSha256 is not null)
        {
            request.PinnedSpkiSha256 = pinnedSpkiSha256;
        }

        return await client.UpdateDeviceConnectionAsync(
                request,
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
