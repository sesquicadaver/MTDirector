using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using ProtoBinding = Mfc.Contracts.Mfc.V1.IncidentResponseAssessmentBinding;
using ProtoSignal = Mfc.Contracts.Mfc.V1.IncidentSignal;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for IncidentService (SEC-06). Fail-closed authz via Application permissions.</summary>
public sealed class IncidentGrpcService : IncidentService.IncidentServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly IngestIncidentSignalUseCase _ingest;
    private readonly BindIncidentResponseAssessmentUseCase _bind;
    private readonly GrpcRequestActorResolver _actors;
    private readonly IHostEnvironment _environment;

    public IncidentGrpcService(
        IngestIncidentSignalUseCase ingest,
        BindIncidentResponseAssessmentUseCase bind,
        GrpcRequestActorResolver actors,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(ingest);
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(environment);
        _ingest = ingest;
        _bind = bind;
        _actors = actors;
        _environment = environment;
    }

    public override async Task<ProtoSignal> IngestIncidentSignal(
        IngestIncidentSignalRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actor = ResolveActor(context);
        IngestIncidentSignalCommand command;
        try
        {
            command = IncidentProtoMapper.ToIngestCommand(request, actor);
        }
        catch (ArgumentException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
        catch (OverflowException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }

        ApplicationResult<IncidentSignalView> result = await _ingest
            .ExecuteAsync(command, context.CancellationToken)
            .ConfigureAwait(false);
        return IncidentProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<ProtoBinding> BindIncidentResponseAssessment(
        BindIncidentResponseAssessmentRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actor = ResolveActor(context);
        BindIncidentResponseAssessmentCommand command;
        try
        {
            command = IncidentProtoMapper.ToBindCommand(request, actor);
        }
        catch (ArgumentException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
        catch (OverflowException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
        catch (DomainInvariantException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }

        ApplicationResult<IncidentResponseAssessmentBindingView> result = await _bind
            .ExecuteAsync(command, context.CancellationToken)
            .ConfigureAwait(false);
        return IncidentProtoMapper.ToProto(Unwrap(result));
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
