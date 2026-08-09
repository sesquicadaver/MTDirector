using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>gRPC surface for Vertical Slice §9.3 SnapshotService (M1-26).</summary>
public sealed class SnapshotGrpcService : SnapshotService.SnapshotServiceBase
{
    public const string ActorMetadataKey = InventoryGrpcService.ActorMetadataKey;

    private const int MaxPageSize = 200;

    private readonly CaptureSnapshotUseCase _capture;
    private readonly ListSnapshotsUseCase _list;
    private readonly GetSnapshotUseCase _get;
    private readonly GetSnapshotSectionUseCase _getSection;
    private readonly CompareSnapshotsUseCase _compare;
    private readonly CaptureProgressHub _progressHub;
    private readonly IHostEnvironment _environment;

    public SnapshotGrpcService(
        CaptureSnapshotUseCase capture,
        ListSnapshotsUseCase list,
        GetSnapshotUseCase get,
        GetSnapshotSectionUseCase getSection,
        CompareSnapshotsUseCase compare,
        CaptureProgressHub progressHub,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(getSection);
        ArgumentNullException.ThrowIfNull(compare);
        ArgumentNullException.ThrowIfNull(progressHub);
        ArgumentNullException.ThrowIfNull(environment);
        _capture = capture;
        _list = list;
        _get = get;
        _getSection = getSection;
        _compare = compare;
        _progressHub = progressHub;
        _environment = environment;
    }

    public override async Task<StartCaptureResponse> StartCapture(
        StartCaptureRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetCase == StartCaptureRequest.TargetOneofCase.NodeId)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(
                ApplicationError.Failed("node capture deferred: StartCapture supports device_id only in M1-26."));
        }

        if (request.TargetCase != StartCaptureRequest.TargetOneofCase.DeviceId)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(
                ApplicationError.Failed("StartCapture requires device_id or node_id."));
        }

        Guid deviceId = ProtoUuid.ToGuid(request.DeviceId);
        Guid idempotencyKey = ProtoUuid.ToGuid(request.IdempotencyKey);
        Guid operationId = _progressHub.Begin(deviceId);
        _progressHub.Publish(operationId, CaptureStage.Queued);

        try
        {
            _progressHub.Publish(operationId, CaptureStage.Persisting);
            ApplicationResult<SnapshotView> result = await _capture.ExecuteAsync(
                new CaptureSnapshotCommand
                {
                    Actor = ResolveActor(context),
                    DeviceId = deviceId,
                    IdempotencyKey = idempotencyKey,
                },
                context.CancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _progressHub.Publish(
                    operationId,
                    CaptureStage.Failed,
                    error: new ErrorDetail
                    {
                        Code = result.Error!.Code,
                        Retryable = false,
                        CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid()),
                        SanitizedDetail = result.Error.Message,
                    });
                throw GrpcApplicationErrorMapper.ToRpcException(result.Error!);
            }

            SnapshotView snapshot = result.Value!;
            _progressHub.Publish(operationId, CaptureStage.Completed, captureId: snapshot.Id);

            StartCaptureResponse response = new()
            {
                OperationId = ProtoUuid.FromGuid(operationId),
                Deduplicated = snapshot.Deduplicated,
                CaptureId = ProtoUuid.FromGuid(snapshot.Id),
            };
            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _progressHub.Publish(operationId, CaptureStage.Canceled);
            throw;
        }
        catch (Exception)
        {
            _progressHub.Publish(
                operationId,
                CaptureStage.Failed,
                error: new ErrorDetail
                {
                    Code = "failed",
                    Retryable = false,
                    CorrelationId = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SanitizedDetail = "capture failed",
                });
            throw;
        }
    }

    public override async Task WatchCapture(
        WatchCaptureRequest request,
        IServerStreamWriter<CaptureProgress> responseStream,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(responseStream);
        Guid operationId = ProtoUuid.ToGuid(request.OperationId);
        await foreach (CaptureProgress progress in _progressHub
                           .WatchAsync(operationId, context.CancellationToken)
                           .ConfigureAwait(false))
        {
            await responseStream.WriteAsync(progress).ConfigureAwait(false);
        }
    }

    public override async Task<ListCapturesResponse> ListCaptures(
        ListCapturesRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        int limit = request.Page?.PageSize is > 0 and var size
            ? Math.Min((int)size, MaxPageSize)
            : 50;
        ApplicationResult<SnapshotListPageView> result = await _list.ExecuteAsync(
            new ListSnapshotsQuery
            {
                Actor = ResolveActor(context),
                DeviceId = ProtoUuid.ToGuid(request.DeviceId),
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(request.Page?.PageToken) ? null : request.Page.PageToken,
            },
            context.CancellationToken).ConfigureAwait(false);
        SnapshotListPageView page = Unwrap(result);
        ListCapturesResponse response = new()
        {
            Page = new PageResponse { NextPageToken = page.NextCursor ?? string.Empty },
        };
        response.Captures.AddRange(page.Items.Select(SnapshotProtoMapper.ToProto));
        return response;
    }

    public override async Task<SnapshotSummary> GetSnapshotSummary(
        GetSnapshotSummaryRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<SnapshotView> result = await _get.ExecuteAsync(
            new GetSnapshotQuery
            {
                Actor = ResolveActor(context),
                SnapshotId = ProtoUuid.ToGuid(request.CaptureId),
            },
            context.CancellationToken).ConfigureAwait(false);
        return SnapshotProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<SnapshotSectionPage> GetSnapshotSection(
        GetSnapshotSectionRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        int limit = request.Page?.PageSize is > 0 and var size
            ? Math.Min((int)size, MaxPageSize)
            : 50;
        ApplicationResult<SnapshotSectionPageView> result = await _getSection.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = ResolveActor(context),
                CaptureId = ProtoUuid.ToGuid(request.CaptureId),
                SectionId = request.SectionId,
                Domain = request.HasDomain ? SnapshotProtoMapper.ToDomain(request.Domain) : null,
                Limit = limit,
                Cursor = string.IsNullOrWhiteSpace(request.Page?.PageToken) ? null : request.Page.PageToken,
            },
            context.CancellationToken).ConfigureAwait(false);
        return SnapshotProtoMapper.ToProto(Unwrap(result));
    }

    public override async Task<DiffPage> CompareSnapshots(
        CompareSnapshotsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ApplicationResult<SnapshotDiffView> result = await _compare.ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = ResolveActor(context),
                LeftSnapshotId = ProtoUuid.ToGuid(request.LeftCaptureId),
                RightSnapshotId = ProtoUuid.ToGuid(request.RightCaptureId),
            },
            context.CancellationToken).ConfigureAwait(false);
        SnapshotDiffView diff = Unwrap(result);

        int limit = request.Page?.PageSize is > 0 and var size
            ? Math.Min((int)size, MaxPageSize)
            : 50;
        int offset = 0;
        if (!string.IsNullOrWhiteSpace(request.Page?.PageToken)
            && int.TryParse(request.Page.PageToken, out int parsed)
            && parsed >= 0)
        {
            offset = parsed;
        }

        IReadOnlyList<SnapshotDiffEntryView> entries = diff.Entries;
        if (offset > entries.Count)
        {
            throw GrpcApplicationErrorMapper.ToRpcException(
                ApplicationError.Failed("Invalid page token."));
        }

        int end = Math.Min(offset + limit, entries.Count);
        DiffPage page = new()
        {
            Identical = diff.Identical,
            NextPageToken = end < entries.Count
                ? end.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty,
        };
        for (int i = offset; i < end; i++)
        {
            page.Entries.Add(SnapshotProtoMapper.ToProto(entries[i]));
        }

        foreach (SnapshotDiffWarningView warning in diff.Warnings)
        {
            page.Warnings.Add(warning.Code + ": " + warning.Message);
        }

        foreach (string field in diff.ChangedFields)
        {
            if (diff.Entries.Count == 0)
            {
                page.Warnings.Add("hash_changed:" + field);
            }
        }

        return page;
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
