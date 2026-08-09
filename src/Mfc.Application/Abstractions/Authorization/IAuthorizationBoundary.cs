namespace Mfc.Application.Abstractions.Authorization;

/// <summary>
/// Thin authorization boundary for use cases. Does not implement controller auth policy —
/// only checks that an actor is allowed to invoke a named permission.
/// </summary>
public interface IAuthorizationBoundary
{
    Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default);
}

public static class ApplicationPermissions
{
    public const string InventoryWrite = "inventory.write";
    public const string InventoryRead = "inventory.read";
    public const string ConnectionProfileWrite = "connection_profile.write";
    public const string DiscoveryRead = "discovery.read";
    public const string SnapshotCapture = "snapshot.capture";
    public const string SnapshotRead = "snapshot.read";
    public const string SnapshotCompare = "snapshot.compare";

    /// <summary>Required to return raw (sanitized) snapshot payload bytes (M1-23 AC#11).</summary>
    public const string SnapshotRawRead = "snapshot.raw.read";
}
