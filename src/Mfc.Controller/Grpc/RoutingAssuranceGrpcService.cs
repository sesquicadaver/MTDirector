using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for RoutingAssuranceService (M7.1-10). Read-only; no routing writes.</summary>
public sealed class RoutingAssuranceGrpcService : RoutingAssuranceService.RoutingAssuranceServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly GetRoutingAssuranceStateUseCase _getState;
    private readonly GrpcRequestActorResolver _actors;
    private readonly IHostEnvironment _environment;

    public RoutingAssuranceGrpcService(
        GetRoutingAssuranceStateUseCase getState,
        GrpcRequestActorResolver actors,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(environment);
        _getState = getState;
        _actors = actors;
        _environment = environment;
    }

    public override async Task<RoutingAssuranceStateDetail> GetDeviceRoutingAssuranceState(
        GetDeviceRoutingAssuranceStateRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<RoutingAssuranceDetailView> result = await _getState.ExecuteAsync(
            new GetRoutingAssuranceStateQuery
            {
                Actor = ResolveActor(context),
                DeviceId = ProtoUuid.ToGuid(request.DeviceId),
            },
            context.CancellationToken).ConfigureAwait(false);
        return RoutingAssuranceProtoMapper.ToProto(Unwrap(result));
    }

    private string ResolveActor(ServerCallContext context) =>
        _actors.Resolve(context, _environment, "development");

    private static T Unwrap<T>(ApplicationResult<T> result)
    {
        if (result.IsFailure)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(result.Error!);
        }

        return result.Value!;
    }
}
