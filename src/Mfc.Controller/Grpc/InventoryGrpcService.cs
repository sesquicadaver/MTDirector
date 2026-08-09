using Grpc.Core;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Common;
using Mfc.Application.Inventory;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain.Inventory.Primitives;
using ProtoDevice = Mfc.Contracts.Mfc.V1.Device;
using ProtoNode = Mfc.Contracts.Mfc.V1.Node;
using ProtoSite = Mfc.Contracts.Mfc.V1.Site;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for Vertical Slice §9.2 InventoryService (M1-25).</summary>
public sealed class InventoryGrpcService : InventoryService.InventoryServiceBase
{
    public const string ActorMetadataKey = "x-mfc-actor";

    private readonly ListSitesUseCase _listSites;
    private readonly ListNodesUseCase _listNodes;
    private readonly CreateSiteUseCase _createSite;
    private readonly CreateNodeUseCase _createNode;
    private readonly GetNodeUseCase _getNode;
    private readonly RegisterDeviceUseCase _registerDevice;
    private readonly UpdateDeviceUseCase _updateDevice;
    private readonly UpdateConnectionProfileUseCase _updateConnection;
    private readonly DiscoverDeviceUseCase _discoverDevice;
    private readonly ValidateDeviceConnectionCoordinator _probeCoordinator;
    private readonly IHostEnvironment _environment;

    public InventoryGrpcService(
        ListSitesUseCase listSites,
        ListNodesUseCase listNodes,
        CreateSiteUseCase createSite,
        CreateNodeUseCase createNode,
        GetNodeUseCase getNode,
        RegisterDeviceUseCase registerDevice,
        UpdateDeviceUseCase updateDevice,
        UpdateConnectionProfileUseCase updateConnection,
        DiscoverDeviceUseCase discoverDevice,
        ValidateDeviceConnectionCoordinator probeCoordinator,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(listSites);
        ArgumentNullException.ThrowIfNull(listNodes);
        ArgumentNullException.ThrowIfNull(createSite);
        ArgumentNullException.ThrowIfNull(createNode);
        ArgumentNullException.ThrowIfNull(getNode);
        ArgumentNullException.ThrowIfNull(registerDevice);
        ArgumentNullException.ThrowIfNull(updateDevice);
        ArgumentNullException.ThrowIfNull(updateConnection);
        ArgumentNullException.ThrowIfNull(discoverDevice);
        ArgumentNullException.ThrowIfNull(probeCoordinator);
        ArgumentNullException.ThrowIfNull(environment);
        _listSites = listSites;
        _listNodes = listNodes;
        _createSite = createSite;
        _createNode = createNode;
        _getNode = getNode;
        _registerDevice = registerDevice;
        _updateDevice = updateDevice;
        _updateConnection = updateConnection;
        _discoverDevice = discoverDevice;
        _probeCoordinator = probeCoordinator;
        _environment = environment;
    }

    public override async Task<ListSitesResponse> ListSites(ListSitesRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actor = ResolveActor(context);
        int limit = request.Page?.PageSize is > 0 and var size ? (int)size : 50;
        ApplicationResult<SiteListPageView> result = await _listSites.ExecuteAsync(
            new ListSitesQuery
            {
                Actor = actor,
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(request.Page?.PageToken) ? null : request.Page.PageToken,
            },
            context.CancellationToken).ConfigureAwait(false);
        SiteListPageView page = Unwrap(result);
        ListSitesResponse response = new()
        {
            Page = new PageResponse { NextPageToken = page.NextCursor ?? string.Empty },
        };
        response.Sites.AddRange(page.Items.Select(InventoryProtoMapper.ToProto));
        return response;
    }

    public override async Task<ListNodesResponse> ListNodes(ListNodesRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actor = ResolveActor(context);
        int limit = request.Page?.PageSize is > 0 and var size ? (int)size : 50;
        ApplicationResult<NodeListPageView> result = await _listNodes.ExecuteAsync(
            new ListNodesQuery
            {
                Actor = actor,
                SiteId = ProtoUuid.ToGuid(request.SiteId),
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(request.Page?.PageToken) ? null : request.Page.PageToken,
            },
            context.CancellationToken).ConfigureAwait(false);
        NodeListPageView page = Unwrap(result);
        ListNodesResponse response = new()
        {
            Page = new PageResponse { NextPageToken = page.NextCursor ?? string.Empty },
        };
        response.Nodes.AddRange(page.Items.Select(InventoryProtoMapper.ToProto));
        return response;
    }

    public override async Task<ProtoSite> CreateSite(CreateSiteRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<SiteView> result = await _createSite.ExecuteAsync(
            new CreateSiteCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                Code = request.Code,
                Name = request.Name,
            },
            context.CancellationToken).ConfigureAwait(false);
        return InventoryProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ProtoNode> CreateNode(CreateNodeRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<NodeView> result = await _createNode.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                SiteId = ProtoUuid.ToGuid(request.SiteId),
                Name = request.Name,
                DeclaredKind = InventoryProtoMapper.ToDomain(request.DeclaredKind),
                DeclaredUplinkMode = InventoryProtoMapper.ToDomain(request.DeclaredUplinkMode),
            },
            context.CancellationToken).ConfigureAwait(false);
        return InventoryProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<NodeDetails> GetNode(GetNodeRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<NodeDetailsView> result = await _getNode.ExecuteAsync(
            new GetNodeQuery
            {
                Actor = ResolveActor(context),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
            },
            context.CancellationToken).ConfigureAwait(false);
        NodeDetailsView details = Unwrap(result);
        NodeDetails response = new()
        {
            Node = InventoryProtoMapper.ToProto(details.Node),
        };
        response.Devices.AddRange(details.Devices.Select(InventoryProtoMapper.ToProto));
        return response;
    }

    public override async Task<ProtoDevice> RegisterDevice(RegisterDeviceRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ushort port = request.ManagementPort == 0
            ? ManagementEndpoint.DefaultApiSslPort
            : checked((ushort)request.ManagementPort);
        ApplicationResult<DeviceView> result = await _registerDevice.ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
                DisplayName = request.DisplayName,
                ManagementHost = request.ManagementHost,
                ManagementPort = port,
                Role = InventoryProtoMapper.ToDomain(request.Role),
            },
            context.CancellationToken).ConfigureAwait(false);
        return InventoryProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ProtoDevice> UpdateDevice(UpdateDeviceRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<DeviceView> result = await _updateDevice.ExecuteAsync(
            new UpdateDeviceCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                DeviceId = ProtoUuid.ToGuid(request.DeviceId),
                ExpectedRowVersion = request.ExpectedRowVersion,
                DisplayName = request.HasDisplayName ? request.DisplayName : null,
                ManagementHost = request.HasManagementHost ? request.ManagementHost : null,
                ManagementPort = request.HasManagementPort ? checked((ushort)request.ManagementPort) : null,
                Enabled = request.HasEnabled ? request.Enabled : null,
                Role = request.Role == DeviceRole.Unspecified
                    ? null
                    : InventoryProtoMapper.ToDomain(request.Role),
            },
            context.CancellationToken).ConfigureAwait(false);
        return InventoryProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<DeviceConnectionSummary> UpdateDeviceConnection(
        UpdateDeviceConnectionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        int connectTimeout = request.ConnectTimeoutMs == 0
            ? Mfc.Domain.Inventory.DeviceConnectionProfile.MinConnectTimeoutMs * 5
            : checked((int)request.ConnectTimeoutMs);
        int commandTimeout = request.CommandTimeoutMs == 0 ? 30_000 : checked((int)request.CommandTimeoutMs);
        long maxResponse = request.MaxResponseBytes == 0 ? 16_777_216L : checked((long)request.MaxResponseBytes);

        ApplicationResult<ConnectionProfileView> result = await _updateConnection.ExecuteAsync(
            new UpsertConnectionProfileCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                DeviceId = ProtoUuid.ToGuid(request.DeviceId),
                Username = request.Username,
                PasswordUtf8 = request.PasswordUtf8.Memory,
                TrustMode = InventoryProtoMapper.ToDomain(request.TrustMode),
                CaProfileRef = request.HasCaProfileRef ? request.CaProfileRef : null,
                PinnedSpkiSha256 = InventoryProtoMapper.ToHash(request.PinnedSpkiSha256),
                ConnectTimeoutMs = connectTimeout,
                CommandTimeoutMs = commandTimeout,
                MaxResponseBytes = maxResponse,
            },
            context.CancellationToken).ConfigureAwait(false);
        return InventoryProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ValidateDeviceConnectionResponse> ValidateDeviceConnection(
        ValidateDeviceConnectionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid deviceId = ProtoUuid.ToGuid(request.DeviceId);
        string actor = ResolveActor(context);

        // Vertical Slice §9.2: ValidateDeviceConnection → DiscoverDeviceUseCase (read-only probe).
        // Does not accept arbitrary RouterOS commands.
        ApplicationResult<DeviceDiscoveryView> result = await _probeCoordinator
            .RunAsync(
                deviceId,
                ct => _discoverDevice.ExecuteAsync(
                    new DiscoverDeviceCommand { Actor = actor, DeviceId = deviceId },
                    ct),
                context.CancellationToken)
            .ConfigureAwait(false);
        return InventoryProtoMapper.ToProto(Unwrap(result));
    }

    private string ResolveActor(ServerCallContext context)
    {
        string? actor = context.RequestHeaders.GetValue(ActorMetadataKey);
        if (!string.IsNullOrWhiteSpace(actor))
        {
            return actor.Trim();
        }

        if (_environment.IsDevelopment())
        {
            return "dev";
        }

        throw GrpcApplicationErrorMapper.ToRpcException(
            ApplicationError.Unauthorized("Missing x-mfc-actor metadata."));
    }

    private static T Unwrap<T>(ApplicationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        throw GrpcApplicationErrorMapper.ToRpcException(result.Error!);
    }
}
