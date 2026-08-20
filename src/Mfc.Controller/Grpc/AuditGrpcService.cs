using Grpc.Core;
using Mfc.Application.Audit;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for AuditService (M6-04). Read-only; no mutate RPCs.</summary>
public sealed class AuditGrpcService : AuditService.AuditServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly ListAuditEventsUseCase _list;
    private readonly IHostEnvironment _environment;

    public AuditGrpcService(ListAuditEventsUseCase list, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(environment);
        _list = list;
        _environment = environment;
    }

    public override async Task<ListAuditEventsResponse> ListAuditEvents(
        ListAuditEventsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<IReadOnlyList<AuditEventView>> result = await _list.ExecuteAsync(
            new ListAuditEventsQuery
            {
                Actor = ResolveActor(context),
                PageSize = request.PageSize == 0
                    ? ListAuditEventsUseCase.DefaultPageSize
                    : (int)request.PageSize,
            },
            context.CancellationToken).ConfigureAwait(false);
        ListAuditEventsResponse response = new();
        response.Events.AddRange(Unwrap(result).Select(AuditProtoMapper.ToProto));
        return response;
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
