using Grpc.Core;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>
/// Fail-closed deadline for Desktop unary gRPC calls (DESK-GRPC-DEADLINE-01).
/// Do not use for long-lived Watch / server-streaming RPCs.
/// </summary>
public static class DesktopGrpcUnaryCall
{
    /// <summary>
    /// Builds <see cref="CallOptions"/> with actor headers, the caller token, and a
    /// deadline of <see cref="DesktopOptions.UnaryCallTimeoutSeconds"/> from now.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="DesktopOptions.UnaryCallTimeoutSeconds"/> is not greater than zero.
    /// </exception>
    public static CallOptions For(DesktopOptions options, Metadata headers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        int seconds = options.UnaryCallTimeoutSeconds;
        if (seconds <= 0)
        {
            throw new InvalidOperationException(
                "Desktop:UnaryCallTimeoutSeconds must be greater than 0 (fail-closed unary deadline).");
        }

        return new CallOptions(
            headers,
            deadline: DateTime.UtcNow.AddSeconds(seconds),
            cancellationToken: cancellationToken);
    }
}
