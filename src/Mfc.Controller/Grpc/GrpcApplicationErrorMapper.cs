using Google.Protobuf;
using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Contracts.Mfc.V1;
using Mfc.Domain.Policy;

namespace Mfc.Controller.Grpc;

/// <summary>Maps application error codes to gRPC status + trailing <see cref="ErrorDetail"/> metadata.</summary>
public static class GrpcApplicationErrorMapper
{
    public const string ErrorDetailMetadataKey = "mfc-error-detail-bin";

    public static RpcException ToRpcException(ApplicationError error, Guid? correlationId = null)
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
            || PolicyEvidenceAnalysisCodes.IsFailedPrecondition(error.Code))
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
        ErrorDetail detail = new()
        {
            Code = error.Code,
            Retryable = retryable,
            CorrelationId = ProtoUuid.FromGuid(correlationId ?? Guid.NewGuid()),
            SanitizedDetail = Sanitize(error.Message),
        };

        Metadata trailers = new()
        {
            { ErrorDetailMetadataKey, detail.ToByteArray() },
        };

        return new RpcException(new Status(statusCode, detail.SanitizedDetail), trailers);
    }

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
