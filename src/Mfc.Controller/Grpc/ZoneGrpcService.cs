using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Zones;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for M2-05 ZoneService (desired zones + Node bindings).</summary>
public sealed class ZoneGrpcService : ZoneService.ZoneServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly CreateZoneDefinitionUseCase _createZone;
    private readonly UpdateZoneDefinitionUseCase _updateZone;
    private readonly ListZoneDefinitionsUseCase _listZones;
    private readonly DeleteZoneDefinitionUseCase _deleteZone;
    private readonly UpsertNodeZoneBindingUseCase _upsertBinding;
    private readonly DeleteNodeZoneBindingUseCase _deleteBinding;
    private readonly ListNodeZoneBindingsUseCase _listBindings;
    private readonly ResolveZonesForDeviceUseCase _resolveDevice;
    private readonly ResolveZonesForNodeUseCase _resolveNode;
    private readonly IHostEnvironment _environment;

    public ZoneGrpcService(
        CreateZoneDefinitionUseCase createZone,
        UpdateZoneDefinitionUseCase updateZone,
        ListZoneDefinitionsUseCase listZones,
        DeleteZoneDefinitionUseCase deleteZone,
        UpsertNodeZoneBindingUseCase upsertBinding,
        DeleteNodeZoneBindingUseCase deleteBinding,
        ListNodeZoneBindingsUseCase listBindings,
        ResolveZonesForDeviceUseCase resolveDevice,
        ResolveZonesForNodeUseCase resolveNode,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(createZone);
        ArgumentNullException.ThrowIfNull(updateZone);
        ArgumentNullException.ThrowIfNull(listZones);
        ArgumentNullException.ThrowIfNull(deleteZone);
        ArgumentNullException.ThrowIfNull(upsertBinding);
        ArgumentNullException.ThrowIfNull(deleteBinding);
        ArgumentNullException.ThrowIfNull(listBindings);
        ArgumentNullException.ThrowIfNull(resolveDevice);
        ArgumentNullException.ThrowIfNull(resolveNode);
        ArgumentNullException.ThrowIfNull(environment);
        _createZone = createZone;
        _updateZone = updateZone;
        _listZones = listZones;
        _deleteZone = deleteZone;
        _upsertBinding = upsertBinding;
        _deleteBinding = deleteBinding;
        _listBindings = listBindings;
        _resolveDevice = resolveDevice;
        _resolveNode = resolveNode;
        _environment = environment;
    }

    public override async Task<ZoneDefinition> CreateZoneDefinition(
        CreateZoneDefinitionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<ZoneDefinitionView> result = await _createZone.ExecuteAsync(
            new CreateZoneDefinitionCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                OwnerScope = ZoneProtoMapper.ToDomain(request.OwnerScope),
                OwnerId = request.OwnerId is null ? null : ProtoUuid.ToGuid(request.OwnerId),
                Key = request.Key,
                Name = request.Name,
                Description = request.HasDescription ? request.Description : null,
            },
            context.CancellationToken).ConfigureAwait(false);
        return ZoneProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ZoneDefinition> UpdateZoneDefinition(
        UpdateZoneDefinitionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<ZoneDefinitionView> result = await _updateZone.ExecuteAsync(
            new UpdateZoneDefinitionCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                ZoneId = ProtoUuid.ToGuid(request.ZoneId),
                ExpectedRowVersion = request.ExpectedRowVersion,
                Name = request.HasName ? request.Name : null,
                Description = request.HasDescription ? request.Description : null,
                ClearDescription = request.ResetDescription,
            },
            context.CancellationToken).ConfigureAwait(false);
        return ZoneProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ListZoneDefinitionsResponse> ListZoneDefinitions(
        ListZoneDefinitionsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<IReadOnlyList<ZoneDefinitionView>> result = await _listZones.ExecuteAsync(
            new ListZoneDefinitionsQuery
            {
                Actor = ResolveActor(context),
                OwnerScope = request.HasOwnerScope ? ZoneProtoMapper.ToDomain(request.OwnerScope) : null,
                OwnerId = request.OwnerId is null ? null : ProtoUuid.ToGuid(request.OwnerId),
            },
            context.CancellationToken).ConfigureAwait(false);
        ListZoneDefinitionsResponse response = new();
        response.Zones.AddRange(Unwrap(result).Select(ZoneProtoMapper.ToProto));
        return response;
    }

    public override async Task<DeleteZoneDefinitionResponse> DeleteZoneDefinition(
        DeleteZoneDefinitionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<bool> result = await _deleteZone.ExecuteAsync(
            new DeleteZoneDefinitionCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                ZoneId = ProtoUuid.ToGuid(request.ZoneId),
                ExpectedRowVersion = request.ExpectedRowVersion,
            },
            context.CancellationToken).ConfigureAwait(false);
        return new DeleteZoneDefinitionResponse { Deleted = Unwrap(result) };
    }

    public override async Task<NodeZoneBinding> UpsertNodeZoneBinding(
        UpsertNodeZoneBindingRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<NodeZoneBindingView> result = await _upsertBinding.ExecuteAsync(
            new UpsertNodeZoneBindingCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
                ZoneId = ProtoUuid.ToGuid(request.ZoneId),
                Kind = ZoneProtoMapper.ToDomain(request.Kind),
                Values = request.Values.ToArray(),
                ExpectedDependencyHash = ZoneProtoMapper.ToHashBytes(request.ExpectedDependencyHash),
                ExpectedRowVersion = request.HasExpectedRowVersion ? request.ExpectedRowVersion : null,
            },
            context.CancellationToken).ConfigureAwait(false);
        return ZoneProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<DeleteNodeZoneBindingResponse> DeleteNodeZoneBinding(
        DeleteNodeZoneBindingRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<bool> result = await _deleteBinding.ExecuteAsync(
            new DeleteNodeZoneBindingCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                BindingId = ProtoUuid.ToGuid(request.BindingId),
                ExpectedRowVersion = request.ExpectedRowVersion,
            },
            context.CancellationToken).ConfigureAwait(false);
        return new DeleteNodeZoneBindingResponse { Deleted = Unwrap(result) };
    }

    public override async Task<ListNodeZoneBindingsResponse> ListNodeZoneBindings(
        ListNodeZoneBindingsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<IReadOnlyList<NodeZoneBindingView>> result = await _listBindings.ExecuteAsync(
            new ListNodeZoneBindingsQuery
            {
                Actor = ResolveActor(context),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
            },
            context.CancellationToken).ConfigureAwait(false);
        ListNodeZoneBindingsResponse response = new();
        response.Bindings.AddRange(Unwrap(result).Select(ZoneProtoMapper.ToProto));
        return response;
    }

    public override async Task<ZoneResolveBatch> ResolveZonesForDevice(
        ResolveZonesForDeviceRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<ZoneResolveBatchView> result = await _resolveDevice.ExecuteAsync(
            new ResolveZonesForDeviceCommand
            {
                Actor = ResolveActor(context),
                DeviceId = ProtoUuid.ToGuid(request.DeviceId),
            },
            context.CancellationToken).ConfigureAwait(false);
        ZoneResolveBatch response = new();
        response.Results.AddRange(Unwrap(result).Results.Select(ZoneProtoMapper.ToProto));
        return response;
    }

    public override async Task<ZoneResolveBatch> ResolveZonesForNode(
        ResolveZonesForNodeRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<ZoneResolveBatchView> result = await _resolveNode.ExecuteAsync(
            new ResolveZonesForNodeCommand
            {
                Actor = ResolveActor(context),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
            },
            context.CancellationToken).ConfigureAwait(false);
        ZoneResolveBatch response = new();
        response.Results.AddRange(Unwrap(result).Results.Select(ZoneProtoMapper.ToProto));
        return response;
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
            return "dev-actor";
        }

        throw GrpcApplicationErrorMapper.ToRpcException(
            ApplicationError.Unauthorized("x-mfc-actor metadata is required."));
    }

    private static T Unwrap<T>(ApplicationResult<T> result)
    {
        if (result.IsFailure)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(result.Error!);
        }

        return result.Value!;
    }
}
