using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Drift;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for DriftService (M6-04). Read-only; no repair RPCs.</summary>
public sealed class DriftGrpcService : DriftService.DriftServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly ListDeviceDriftEventsUseCase _listDevice;
    private readonly GetDriftEventUseCase _getEvent;
    private readonly IHostEnvironment _environment;

    public DriftGrpcService(
        ListDeviceDriftEventsUseCase listDevice,
        GetDriftEventUseCase getEvent,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(listDevice);
        ArgumentNullException.ThrowIfNull(getEvent);
        ArgumentNullException.ThrowIfNull(environment);
        _listDevice = listDevice;
        _getEvent = getEvent;
        _environment = environment;
    }

    public override async Task<ListDeviceDriftEventsResponse> ListDeviceDriftEvents(
        ListDeviceDriftEventsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<IReadOnlyList<DriftEventView>> result = await _listDevice.ExecuteAsync(
            new ListDeviceDriftEventsQuery
            {
                Actor = ResolveActor(context),
                DeviceId = ProtoUuid.ToGuid(request.DeviceId),
            },
            context.CancellationToken).ConfigureAwait(false);
        ListDeviceDriftEventsResponse response = new();
        response.Events.AddRange(Unwrap(result).Select(DriftProtoMapper.ToProto));
        return response;
    }

    public override async Task<global::Mfc.Contracts.Mfc.V1.DriftEvent> GetDriftEvent(
        GetDriftEventRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<DriftEventView> result = await _getEvent.ExecuteAsync(
            new GetDriftEventQuery
            {
                Actor = ResolveActor(context),
                DriftEventId = ProtoUuid.ToGuid(request.DriftEventId),
            },
            context.CancellationToken).ConfigureAwait(false);
        return DriftProtoMapper.ToProto(Unwrap(result));
    }

    private string ResolveActor(ServerCallContext context)
    {
        string? actor = context.RequestHeaders.GetValue(ActorMetadataKey);
        if (string.IsNullOrWhiteSpace(actor))
        {
            if (_environment.IsDevelopment())
            {
                return "development";
            }

            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Unauthorized());
        }

        return actor;
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
