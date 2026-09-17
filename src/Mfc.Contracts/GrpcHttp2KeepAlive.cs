namespace Mfc.Contracts;

/// <summary>
/// Shared gRPC HTTP/2 keepalive intervals (CTRL-GRPC-KEEPALIVE-01).
/// Finite fail-closed pings keep long-lived Watch streams alive through idle NAT/LB paths —
/// never <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> / <see cref="System.TimeSpan.MaxValue"/> (defaults).
/// </summary>
public static class GrpcHttp2KeepAlive
{
    /// <summary>
    /// Idle interval before an HTTP/2 PING is sent (60 seconds).
    /// </summary>
    public static readonly TimeSpan PingDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum wait for a PING ACK before the connection is treated as broken (30 seconds).
    /// </summary>
    public static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(30);
}
