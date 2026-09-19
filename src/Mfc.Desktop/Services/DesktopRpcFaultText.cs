using Google.Protobuf;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>
/// Maps Controller <c>mfc-error-detail-bin</c> trailers into operator <c>ErrorText</c>
/// (DESK-RPC-FAULT-01). Does not change unary deadlines or Watch streams.
/// </summary>
public static class DesktopRpcFaultText
{
    /// <summary>Binary trailer key written by Controller <c>GrpcApplicationErrorMapper</c>.</summary>
    public const string ErrorDetailMetadataKey = "mfc-error-detail-bin";

    /// <summary>
    /// Operator fault text. Prefers structured code + correlation id from the trailer.
    /// When the trailer is absent or unreadable, returns <see cref="Status.Detail"/>,
    /// or the status code when that detail is empty (client <see cref="StatusCode.DeadlineExceeded"/>).
    /// </summary>
    public static string Format(RpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!TryReadDetail(exception, out ErrorDetail? detail) || detail is null)
        {
            return NonEmptyDetailOrStatus(exception);
        }

        string narrative = FirstNonEmpty(
            detail.SanitizedDetail,
            exception.Status.Detail,
            exception.StatusCode.ToString());
        string code = string.IsNullOrWhiteSpace(detail.Code)
            ? exception.StatusCode.ToString()
            : detail.Code.Trim();
        if (TryCorrelation(detail, out string correlation))
        {
            string retry = detail.Retryable ? ", retryable" : "";
            return $"{code} (correlation {correlation}{retry}): {narrative}";
        }

        return detail.Retryable
            ? $"{code} (retryable): {narrative}"
            : $"{code}: {narrative}";
    }

    /// <summary>
    /// Operator text for a caught exception (DESK-SVC-FAULT-01 service <c>Error</c>;
    /// DESK-CONN-DISC-01 connect/reconnect <c>Disconnected</c>).
    /// An <see cref="RpcException"/> uses <see cref="Format(RpcException)"/> so the correlation id is kept.
    /// Other exceptions keep <see cref="Exception.Message"/>.
    /// </summary>
    public static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is RpcException rpc)
        {
            return Format(rpc);
        }

        return exception.Message;
    }

    private static bool TryReadDetail(RpcException exception, out ErrorDetail? detail)
    {
        detail = null;
        byte[]? bytes;
        try
        {
            bytes = exception.Trailers.GetValueBytes(ErrorDetailMetadataKey);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (bytes is null || bytes.Length == 0)
        {
            return false;
        }

        try
        {
            detail = ErrorDetail.Parser.ParseFrom(bytes);
            return true;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }

    private static bool TryCorrelation(ErrorDetail detail, out string correlation)
    {
        correlation = "";
        if (detail.CorrelationId is null || detail.CorrelationId.Value.Length != 16)
        {
            return false;
        }

        correlation = DesktopProtoUuid.ToGuid(detail.CorrelationId).ToString("D");
        return true;
    }

    private static string NonEmptyDetailOrStatus(RpcException exception)
        => string.IsNullOrWhiteSpace(exception.Status.Detail)
            ? exception.StatusCode.ToString()
            : exception.Status.Detail;

    private static string FirstNonEmpty(string? sanitized, string statusDetail, string statusCode)
    {
        if (!string.IsNullOrWhiteSpace(sanitized))
        {
            return sanitized.Trim();
        }

        if (!string.IsNullOrWhiteSpace(statusDetail))
        {
            return statusDetail;
        }

        return statusCode;
    }
}
