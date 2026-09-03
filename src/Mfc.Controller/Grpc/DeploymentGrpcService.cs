using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Deployment;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for DeploymentService (M4-12).</summary>
public sealed class DeploymentGrpcService : DeploymentService.DeploymentServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly CreateDeploymentPlanUseCase _createPlan;
    private readonly StartDeploymentUseCase _start;
    private readonly RollbackDeploymentWorkflowUseCase _rollback;
    private readonly GetDeploymentRecoveryStatusUseCase _recovery;
    private readonly DeploymentProgressHub _progress;
    private readonly GrpcRequestActorResolver _actors;
    private readonly IHostEnvironment _environment;

    public DeploymentGrpcService(
        CreateDeploymentPlanUseCase createPlan,
        StartDeploymentUseCase start,
        RollbackDeploymentWorkflowUseCase rollback,
        GetDeploymentRecoveryStatusUseCase recovery,
        DeploymentProgressHub progress,
        GrpcRequestActorResolver actors,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(createPlan);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(rollback);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(environment);
        _createPlan = createPlan;
        _start = start;
        _rollback = rollback;
        _recovery = recovery;
        _progress = progress;
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

    public override async Task<DeploymentOperationSummary> Start(
        StartDeploymentRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<DeploymentOperationSummaryView> result = await _start.ExecuteAsync(
            new StartDeploymentCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                PlanId = ProtoUuid.ToGuid(request.PlanId),
                PlanHash = DeploymentProtoMapper.ToHashBytes(request.PlanHash),
                PacketPathPairs = request.PacketPathPairs.Select(DeploymentProtoMapper.ToPacketPath).ToArray(),
            },
            context.CancellationToken).ConfigureAwait(false);
        DeploymentOperationSummaryView view = Unwrap(result);
        _progress.Ensure(view.OperationId);
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
        ApplicationResult<DeploymentOperationSummaryView> result = await _rollback.ExecuteAsync(
            new RollbackDeploymentCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                OperationId = ProtoUuid.ToGuid(request.OperationId),
            },
            context.CancellationToken).ConfigureAwait(false);
        DeploymentOperationSummaryView view = Unwrap(result);
        _progress.Ensure(view.OperationId);
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

    private static T Unwrap<T>(ApplicationResult<T> result)
    {
        if (result.IsFailure)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(result.Error!);
        }

        return result.Value!;
    }
}
