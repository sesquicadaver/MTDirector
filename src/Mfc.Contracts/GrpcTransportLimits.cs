namespace Mfc.Contracts;

/// <summary>
/// Shared gRPC transport message-size ceilings (CTRL-GRPC-MSGSIZE-01).
/// Aligned with domain <c>RawSnapshotLimits.MaxSnapshotBytes</c> (256 MiB) so Controller
/// and Desktop fail closed at the same finite bound — never unlimited.
/// </summary>
public static class GrpcTransportLimits
{
    /// <summary>
    /// Maximum gRPC receive/send message size in bytes (256 MiB = 268435456).
    /// Matches product raw-snapshot assembly ceiling.
    /// </summary>
    public const int MaxMessageBytes = 256 * 1024 * 1024;
}
