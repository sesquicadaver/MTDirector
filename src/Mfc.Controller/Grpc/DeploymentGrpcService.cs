using Grpc.Core;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for DeploymentService (M4-12).</summary>
public sealed class DeploymentGrpcService : DeploymentService.DeploymentServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly CreateDeploymentPlanUseCase _createPlan;
    private readonly CreateDeploymentPlanFromSealedArtifactsUseCase _createPlanFromSealed;
    private readonly StartDeploymentUseCase _start;
    private readonly RollbackDeploymentWorkflowUseCase _rollback;
    private readonly GetDeploymentRecoveryStatusUseCase _recovery;
    private readonly IDeploymentStore _operations;
    private readonly DeploymentProgressHub _progress;
    private readonly IAuthorizationBoundary _auth;
    private readonly GrpcRequestActorResolver _actors;
    private readonly IHostEnvironment _environment;

    public DeploymentGrpcService(
        CreateDeploymentPlanUseCase createPlan,
        CreateDeploymentPlanFromSealedArtifactsUseCase createPlanFromSealed,
        StartDeploymentUseCase start,
        RollbackDeploymentWorkflowUseCase rollback,
        GetDeploymentRecoveryStatusUseCase recovery,
        IDeploymentStore operations,
        DeploymentProgressHub progress,
        IAuthorizationBoundary auth,
        GrpcRequestActorResolver actors,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(createPlan);
        ArgumentNullException.ThrowIfNull(createPlanFromSealed);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(rollback);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(environment);
        _createPlan = createPlan;
        _createPlanFromSealed = createPlanFromSealed;
        _start = start;
        _rollback = rollback;
        _recovery = recovery;
        _operations = operations;
        _progress = progress;
        _auth = auth;
        _actors = actors;
        _environment = environment;
    }

    public override async Task<DeploymentPlanSummary> CreatePlan(
        CreateDeploymentPlanRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            List<DeviceDeploymentPlan> devicePlans = request.Devices.Select(DeploymentProtoMapper.ToDevicePlan).ToList();
            ApplicationResult<DeploymentPlanSummaryView> result = await _createPlan.ExecuteAsync(
                new CreateDeploymentPlanCommand
                {
                    Actor = ResolveActor(context),
                    IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                    NodeId = ProtoUuid.ToGuid(request.NodeId),
                    LogicalPolicyHash = DeploymentProtoMapper.ToHashBytes(request.LogicalPolicyHash),
                    AnalysisBundleHash = DeploymentProtoMapper.ToHashBytes(request.AnalysisBundleHash),
                    TopologyProjectionHash = DeploymentProtoMapper.ToHashBytes(request.TopologyProjectionHash),
                    DevicePlans = devicePlans,
                },
                context.CancellationToken).ConfigureAwait(false);
            return DeploymentProtoMapper.ToProto(Unwrap(result));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (DomainInvariantException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
    }

    public override async Task<CreateDeploymentPlanFromSealedArtifactsResponse> CreatePlanFromSealedArtifacts(
        CreateDeploymentPlanFromSealedArtifactsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ApplicationResult<SealedDeploymentPlanResult> result = await _createPlanFromSealed.ExecuteAsync(
                new CreateDeploymentPlanFromSealedArtifactsCommand
                {
                    Actor = ResolveActor(context),
                    IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                    NodeId = ProtoUuid.ToGuid(request.NodeId),
                    AnalysisRunId = ProtoUuid.ToGuid(request.AnalysisRunId),
                    Devices = request.Devices.Select(static d => new Application.Deployment.SealedArtifactDeviceRef
                    {
                        DeviceId = ProtoUuid.ToGuid(d.DeviceId),
                        NewArtifactResourceHash = DeploymentProtoMapper.ToHashBytes(d.NewArtifactResourceHash),
                    }).ToList(),
                },
                context.CancellationToken).ConfigureAwait(false);
            SealedDeploymentPlanResult sealedResult = Unwrap(result);
            CreateDeploymentPlanFromSealedArtifactsResponse response = new()
            {
                Plan = DeploymentProtoMapper.ToProto(sealedResult.Plan),
            };
            foreach (PacketPathPairFact pair in sealedResult.CapturePacketPathPairs)
            {
                response.CapturePacketPathPairs.Add(DeploymentProtoMapper.ToProto(pair));
            }

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (DomainInvariantException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
    }

    public override async Task<DeploymentOperationSummary> Start(
        StartDeploymentRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actor = ResolveActor(context);
        ApplicationResult<DeploymentOperationSummaryView> result = await _start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = actor,
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                PlanId = ProtoUuid.ToGuid(request.PlanId),
                PlanHash = DeploymentProtoMapper.ToHashBytes(request.PlanHash),
                PacketPathPairs = request.PacketPathPairs.Select(DeploymentProtoMapper.ToPacketPath).ToArray(),
            },
            context.CancellationToken).ConfigureAwait(false);
        DeploymentOperationSummaryView view = Unwrap(result);
        _progress.Ensure(view.OperationId, actor);
        foreach (string entry in view.Timeline)
        {
            _progress.Publish(view.OperationId, view.State, view.ErrorCode, entry);
        }

        _progress.Publish(view.OperationId, view.State, view.ErrorCode);
        return DeploymentProtoMapper.ToProto(view);
    }

    public override async Task Watch(
        WatchDeploymentRequest request,
        IServerStreamWriter<DeploymentProgress> responseStream,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid operationId = ProtoUuid.ToGuid(request.OperationId);
        await EnsureWatchAuthorizedAsync(context, operationId).ConfigureAwait(false);
        await foreach (DeploymentProgress progress in _progress.WatchAsync(operationId, context.CancellationToken)
                           .ConfigureAwait(false))
        {
            await responseStream.WriteAsync(progress).ConfigureAwait(false);
        }
    }

    public override async Task<DeploymentOperationSummary> Rollback(
        RollbackDeploymentRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        string actor = ResolveActor(context);
        ApplicationResult<DeploymentOperationSummaryView> result = await _rollback.ExecuteAsync(
            new RollbackDeploymentCommand
            {
                Actor = actor,
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                OperationId = ProtoUuid.ToGuid(request.OperationId),
            },
            context.CancellationToken).ConfigureAwait(false);
        DeploymentOperationSummaryView view = Unwrap(result);
        _progress.Ensure(view.OperationId, actor);
        foreach (string entry in view.Timeline)
        {
            _progress.Publish(view.OperationId, view.State, view.ErrorCode, entry);
        }

        _progress.Publish(view.OperationId, view.State, view.ErrorCode);
        return DeploymentProtoMapper.ToProto(view);
    }

    public override async Task<DeploymentRecoveryStatus> GetRecoveryStatus(
        GetDeploymentRecoveryStatusRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ApplicationResult<DeploymentRecoveryStatusView> result = await _recovery.ExecuteAsync(
                new GetDeploymentRecoveryStatusQuery
                {
                    Actor = ResolveActor(context),
                    NodeId = ProtoUuid.ToGuid(request.NodeId),
                    OperationId = ProtoUuid.ToNullableGuid(request.OperationId),
                    LiveJumpsByMarker = DeploymentProtoMapper.ToLiveJumps(request.Jumps),
                    WatchdogSchedulers = DeploymentProtoMapper.ToWatchdogs(request.WatchdogSchedulers),
                },
                context.CancellationToken).ConfigureAwait(false);
            return DeploymentProtoMapper.ToProto(Unwrap(result));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (DomainInvariantException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Validation(ex.Message));
        }
    }

    private string ResolveActor(ServerCallContext context) =>
        _actors.Resolve(context, _environment, "development");

    private async Task EnsureWatchAuthorizedAsync(ServerCallContext context, Guid operationId)
    {
        string actor = ResolveActor(context);
        try
        {
            await _auth.EnsureAllowedAsync(
                    actor,
                    ApplicationPermissions.DeploymentRead,
                    context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Forbidden(ex.Message));
        }

        if (_progress.TryGetOwnerActor(operationId, out string hubOwner))
        {
            if (!string.Equals(hubOwner, actor, StringComparison.Ordinal))
            {
                throw GrpcApplicationErrorMapper.ToRpcException(
                    ApplicationError.Forbidden("Watch requires the operation owner."));
            }

            return;
        }

        DeploymentOperation? operation = await _operations
            .GetOperationAsync(new DeploymentOperationId(operationId), context.CancellationToken)
            .ConfigureAwait(false);
        if (operation is not null
            && operation.CreatedBy.Value == ActorKey.FromActor(actor))
        {
            _progress.Ensure(operationId, actor);
            return;
        }

        throw GrpcApplicationErrorMapper.ToRpcException(
            ApplicationError.Forbidden("Watch requires the operation owner."));
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
