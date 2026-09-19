using Google.Protobuf;
using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain.Policy;
using Microsoft.Extensions.Logging;

namespace Mfc.Controller.Grpc;

/// <summary>
/// Maps application error codes to gRPC status + trailing <see cref="ErrorDetail"/> metadata.
/// DI injects <see cref="ILogger{T}"/>; <see cref="BindForStaticCallSites"/> publishes that logger
/// to the existing static call sites so journald can be joined to Desktop <c>ErrorText</c>
/// (CTRL-ERRDETAIL-LOG-01). The trailer contract is unchanged.
/// </summary>
public sealed partial class GrpcApplicationErrorMapper
{
    public const string ErrorDetailMetadataKey = "mfc-error-detail-bin";

    private static ILogger? _boundLogger;

    private readonly ILogger<GrpcApplicationErrorMapper> _logger;

    public GrpcApplicationErrorMapper(ILogger<GrpcApplicationErrorMapper> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Publishes the injected logger for static <see cref="ToRpcException"/> call sites.
    /// Controller host calls this once after the container is built.
    /// </summary>
    public void BindForStaticCallSites() => _boundLogger = _logger;

    /// <summary>Drops the static logger so unit tests do not leak a disposed provider.</summary>
    public void UnbindStaticCallSites()
    {
        if (ReferenceEquals(_boundLogger, _logger))
        {
            _boundLogger = null;
        }
    }

    public static RpcException ToRpcException(ApplicationError error, Guid? correlationId = null)
        => Map(_boundLogger, error, correlationId);

    private static RpcException Map(ILogger? logger, ApplicationError error, Guid? correlationId)
    {
        ArgumentNullException.ThrowIfNull(error);
        StatusCode statusCode;
        bool retryable;
        if (error.Code.StartsWith("POLICY_COMPOSE_", StringComparison.Ordinal)
            || error.Code.StartsWith("POLICY_EXCEPTION_", StringComparison.Ordinal)
            || error.Code.StartsWith("PREDICATE_", StringComparison.Ordinal)
            || error.Code.StartsWith("RULE_", StringComparison.Ordinal)
            || PolicyAnalysisCodes.IsSequenceComposeFailure(error.Code)
            || ActualFilterAnalysisCodes.IsFailedPrecondition(error.Code)
            || PacketPathAnalysisCodes.IsFailedPrecondition(error.Code)
            || ManagementPathAnalysisCodes.IsFailedPrecondition(error.Code)
            || TopologyDependencyAnalysisCodes.IsFailedPrecondition(error.Code)
            || FastTrackAnalysisCodes.IsFailedPrecondition(error.Code)
            || PolicyEvidenceAnalysisCodes.IsFailedPrecondition(error.Code)
            || PolicyApprovalCodes.IsFailedPrecondition(error.Code)
            || PolicyCompilerCodes.IsFailedPrecondition(error.Code)
            || error.Code.StartsWith("ONBOARDING_", StringComparison.Ordinal)
            || error.Code.StartsWith("BOOTSTRAP_", StringComparison.Ordinal)
            || error.Code.StartsWith("ANCHOR_", StringComparison.Ordinal)
            || error.Code.StartsWith("DEPLOYMENT_", StringComparison.Ordinal)
            || error.Code.StartsWith("MANAGEMENT_", StringComparison.Ordinal)
            || error.Code.StartsWith("MFC_", StringComparison.Ordinal)
            || error.Code.StartsWith("DEVICE_", StringComparison.Ordinal)
            || error.Code.StartsWith("SCHEDULER_", StringComparison.Ordinal))
        {
            statusCode = StatusCode.FailedPrecondition;
            retryable = false;
        }
        else
        {
            statusCode = error.Code switch
            {
                "unauthorized" => StatusCode.Unauthenticated,
                "forbidden" => StatusCode.PermissionDenied,
                "not_found" => StatusCode.NotFound,
                "conflict" => StatusCode.Aborted,
                "validation" => StatusCode.InvalidArgument,
                "failed" => StatusCode.FailedPrecondition,
                "dependency" => StatusCode.Unavailable,
                "snapshot_unstable" => StatusCode.Aborted,
                "snapshot_too_large" => StatusCode.ResourceExhausted,
                "snapshots_from_different_devices" => StatusCode.InvalidArgument,
                "snapshot_not_completed" => StatusCode.FailedPrecondition,
                _ => StatusCode.Internal,
            };
            retryable = statusCode is StatusCode.Unavailable or StatusCode.Aborted;
        }

        Guid id = correlationId ?? Guid.NewGuid();
        ErrorDetail detail = new()
        {
            Code = error.Code,
            Retryable = retryable,
            CorrelationId = ProtoUuid.FromGuid(id),
            SanitizedDetail = Sanitize(error.Message),
        };

        if (logger is not null)
        {
            LogFault(logger, detail.Code, statusCode.ToString(), id.ToString("D"), retryable);
        }

        Metadata trailers = new()
        {
            { ErrorDetailMetadataKey, detail.ToByteArray() },
        };

        return new RpcException(new Status(statusCode, detail.SanitizedDetail), trailers);
    }

    [LoggerMessage(
        EventId = 5301,
        Level = LogLevel.Warning,
        Message = "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}")]
    private static partial void LogFault(
        ILogger logger,
        string code,
        string status,
        string correlationId,
        bool retryable);

    private static string Sanitize(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "request failed";
        }

        string trimmed = message.Trim();
        if (trimmed.Contains("password", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("ciphertext", StringComparison.OrdinalIgnoreCase))
        {
            return "request failed (sanitized)";
        }

        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }
}
