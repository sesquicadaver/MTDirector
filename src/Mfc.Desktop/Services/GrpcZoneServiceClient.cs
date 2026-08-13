using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC ZoneService client bound to the current controller channel.</summary>
public sealed class GrpcZoneServiceClient : IZoneServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcZoneServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<ZoneDefinition>> ListZoneDefinitionsAsync(
        PolicyOwnerScope? ownerScope = null,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        ListZoneDefinitionsRequest request = new();
        if (ownerScope is not null)
        {
            request.OwnerScope = ownerScope.Value;
        }

        if (ownerId is Guid id)
        {
            request.OwnerId = DesktopProtoUuid.FromGuid(id);
        }

        ListZoneDefinitionsResponse response = await client.ListZoneDefinitionsAsync(
                request,
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Zones.ToArray();
    }

    public async Task<ZoneDefinition> CreateZoneDefinitionAsync(
        PolicyOwnerScope ownerScope,
        Guid? ownerId,
        string key,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        CreateZoneDefinitionRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            OwnerScope = ownerScope,
            Key = key,
            Name = name,
        };
        if (ownerId is Guid id)
        {
            request.OwnerId = DesktopProtoUuid.FromGuid(id);
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            request.Description = description;
        }

        return await client.CreateZoneDefinitionAsync(
                request,
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ZoneDefinition> UpdateZoneDefinitionAsync(
        Guid zoneId,
        ulong expectedRowVersion,
        string? name,
        string? description,
        bool resetDescription,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        UpdateZoneDefinitionRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            ZoneId = DesktopProtoUuid.FromGuid(zoneId),
            ExpectedRowVersion = expectedRowVersion,
            ResetDescription = resetDescription,
        };
        if (name is not null)
        {
            request.Name = name;
        }

        if (description is not null)
        {
            request.Description = description;
        }

        return await client.UpdateZoneDefinitionAsync(
                request,
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteZoneDefinitionAsync(
        Guid zoneId,
        ulong expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        _ = await client.DeleteZoneDefinitionAsync(
                new DeleteZoneDefinitionRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    ZoneId = DesktopProtoUuid.FromGuid(zoneId),
                    ExpectedRowVersion = expectedRowVersion,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NodeZoneBinding>> ListNodeZoneBindingsAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        ListNodeZoneBindingsResponse response = await client.ListNodeZoneBindingsAsync(
                new ListNodeZoneBindingsRequest { NodeId = DesktopProtoUuid.FromGuid(nodeId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Bindings.ToArray();
    }

    public async Task<NodeZoneBinding> UpsertNodeZoneBindingAsync(
        Guid nodeId,
        Guid zoneId,
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        ulong? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        UpsertNodeZoneBindingRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            NodeId = DesktopProtoUuid.FromGuid(nodeId),
            ZoneId = DesktopProtoUuid.FromGuid(zoneId),
            Kind = kind,
        };
        request.Values.AddRange(values);
        if (expectedRowVersion is ulong version)
        {
            request.ExpectedRowVersion = version;
        }

        return await client.UpsertNodeZoneBindingAsync(
                request,
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteNodeZoneBindingAsync(
        Guid bindingId,
        ulong expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        _ = await client.DeleteNodeZoneBindingAsync(
                new DeleteNodeZoneBindingRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    BindingId = DesktopProtoUuid.FromGuid(bindingId),
                    ExpectedRowVersion = expectedRowVersion,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ZoneResolveBatch> ResolveZonesForNodeAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        return await client.ResolveZonesForNodeAsync(
                new ResolveZonesForNodeRequest { NodeId = DesktopProtoUuid.FromGuid(nodeId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ZoneResolveBatch> ResolveZonesForDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        ZoneService.ZoneServiceClient client = CreateClient();
        return await client.ResolveZonesForDeviceAsync(
                new ResolveZonesForDeviceRequest { DeviceId = DesktopProtoUuid.FromGuid(deviceId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private ZoneService.ZoneServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new ZoneService.ZoneServiceClient(channel);
    }

    private Metadata ActorHeaders()
        => new() { { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor } };
}
