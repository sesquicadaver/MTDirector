using Grpc.Core;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Onboarding;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using DomainNode = Mfc.Domain.Inventory.Node;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for OnboardingService (M5-09).</summary>
public sealed class OnboardingGrpcService : OnboardingService.OnboardingServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly ValidateOnboardingPrerequisitesWorkflowUseCase _validate;
    private readonly CreateOnboardingPlanUseCase _createPlan;
    private readonly StartOnboardingUseCase _start;
    private readonly RollbackOnboardingWorkflowUseCase _rollback;
    private readonly GetOnboardingRecoveryStatusUseCase _recovery;
    private readonly INodeStore _nodes;
    private readonly OnboardingProgressHub _progress;
    private readonly IHostEnvironment _environment;

    public OnboardingGrpcService(
        ValidateOnboardingPrerequisitesWorkflowUseCase validate,
        CreateOnboardingPlanUseCase createPlan,
        StartOnboardingUseCase start,
        RollbackOnboardingWorkflowUseCase rollback,
        GetOnboardingRecoveryStatusUseCase recovery,
        INodeStore nodes,
        OnboardingProgressHub progress,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(createPlan);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(rollback);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(environment);
        _validate = validate;
        _createPlan = createPlan;
        _start = start;
        _rollback = rollback;
        _recovery = recovery;
        _nodes = nodes;
        _progress = progress;
        _environment = environment;
    }

    public override async Task<OnboardingPrerequisiteReport> ValidatePrerequisites(
        ValidateOnboardingPrerequisitesRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<OnboardingPrerequisiteReportView> result = await _validate.ExecuteAsync(
            new ValidateOnboardingPrerequisitesCommand
            {
                Actor = ResolveActor(context),
                NodeId = ProtoUuid.ToGuid(request.NodeId),
                Facts = request.Devices.Select(OnboardingProtoMapper.ToFacts).ToArray(),
            },
            context.CancellationToken).ConfigureAwait(false);
        return OnboardingProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<OnboardingPlanSummary> CreatePlan(
        CreateOnboardingPlanRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid nodeId = ProtoUuid.ToGuid(request.NodeId);
        try
        {
            DomainNode? node = await _nodes.GetAsync(new NodeId(nodeId), context.CancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                throw GrpcApplicationErrorMapper.ToRpcException(
                    ApplicationError.NotFound($"Node '{nodeId}' not found."));
            }

            List<DeviceOnboardingPlan> devicePlans = [];
            foreach (OnboardingDevicePlanInput input in request.Devices)
            {
                devicePlans.Add(OnboardingProtoMapper.ToDevicePlan(input, node.DeclaredKind));
            }

            ApplicationResult<OnboardingPlanSummaryView> result = await _createPlan.ExecuteAsync(
                new CreateOnboardingPlanCommand
                {
                    Actor = ResolveActor(context),
                    IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                    NodeId = nodeId,
                    NodeMembershipHash = OnboardingProtoMapper.ToHashBytes(request.NodeMembershipHash),
                    TopologyProjectionHash = OnboardingProtoMapper.ToHashBytes(request.TopologyProjectionHash),
                    DevicePlans = devicePlans,
                },
                context.CancellationToken).ConfigureAwait(false);
            return OnboardingProtoMapper.ToProto(Unwrap(result));
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

    public override async Task<OnboardingOperationSummary> Start(
        StartOnboardingRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<OnboardingOperationSummaryView> result = await _start.ExecuteAsync(
            new StartOnboardingCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                PlanId = ProtoUuid.ToGuid(request.PlanId),
                PlanHash = OnboardingProtoMapper.ToHashBytes(request.PlanHash),
            },
            context.CancellationToken).ConfigureAwait(false);
        OnboardingOperationSummaryView view = Unwrap(result);
        _progress.Ensure(view.OperationId);
        foreach (string entry in view.Timeline)
        {
            _progress.Publish(view.OperationId, view.State, view.ErrorCode, entry);
        }

        _progress.Publish(view.OperationId, view.State, view.ErrorCode);
        return OnboardingProtoMapper.ToProto(view);
    }

    public override async Task Watch(
        WatchOnboardingRequest request,
        IServerStreamWriter<OnboardingProgress> responseStream,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid operationId = ProtoUuid.ToGuid(request.OperationId);
        await foreach (OnboardingProgress progress in _progress.WatchAsync(operationId, context.CancellationToken)
                           .ConfigureAwait(false))
        {
            await responseStream.WriteAsync(progress).ConfigureAwait(false);
        }
    }

    public override async Task<OnboardingOperationSummary> Rollback(
        RollbackOnboardingRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<OnboardingOperationSummaryView> result = await _rollback.ExecuteAsync(
            new RollbackOnboardingCommand
            {
                Actor = ResolveActor(context),
                IdempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey),
                OperationId = ProtoUuid.ToGuid(request.OperationId),
            },
            context.CancellationToken).ConfigureAwait(false);
        OnboardingOperationSummaryView view = Unwrap(result);
        _progress.Ensure(view.OperationId);
        foreach (string entry in view.Timeline)
        {
            _progress.Publish(view.OperationId, view.State, view.ErrorCode, entry);
        }

        _progress.Publish(view.OperationId, view.State, view.ErrorCode);
        return OnboardingProtoMapper.ToProto(view);
    }

    public override async Task<OnboardingRecoveryStatus> GetRecoveryStatus(
        GetOnboardingRecoveryStatusRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ApplicationResult<OnboardingRecoveryStatusView> result = await _recovery.ExecuteAsync(
                new GetOnboardingRecoveryStatusQuery
                {
                    Actor = ResolveActor(context),
                    NodeId = ProtoUuid.ToGuid(request.NodeId),
                    OperationId = ProtoUuid.ToNullableGuid(request.OperationId),
                    LiveAnchors = OnboardingProtoMapper.ToLiveAnchors(request.Anchors),
                    WatchdogNames = request.WatchdogSchedulers.Count == 0
                        ? null
                        : OnboardingProtoMapper.ToWatchdogNames(request.WatchdogSchedulers),
                },
                context.CancellationToken).ConfigureAwait(false);
            return OnboardingProtoMapper.ToProto(Unwrap(result));
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
