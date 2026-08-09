using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Device belonging to a Node. Management endpoint is typed (<see cref="ManagementEndpoint"/>).
/// </summary>
public sealed class Device
{
    public DeviceId Id { get; }

    public NodeId NodeId { get; }

    public NonEmptyName DisplayName { get; private set; }

    public ManagementEndpoint ManagementEndpoint { get; private set; }

    public DeviceRole Role { get; private set; }

    public bool Enabled { get; private set; }

    public SupportState? LastSupportState { get; private set; }

    /// <summary>Last successfully persisted capture for this device, if any.</summary>
    public Guid? LastCompletedCaptureId { get; private set; }

    public ulong RowVersion { get; private set; }

    private Device(
        DeviceId id,
        NodeId nodeId,
        NonEmptyName displayName,
        ManagementEndpoint managementEndpoint,
        DeviceRole role,
        bool enabled,
        SupportState? lastSupportState,
        Guid? lastCompletedCaptureId,
        ulong rowVersion)
    {
        Id = id;
        NodeId = nodeId;
        DisplayName = displayName;
        ManagementEndpoint = managementEndpoint;
        Role = role;
        Enabled = enabled;
        LastSupportState = lastSupportState;
        LastCompletedCaptureId = lastCompletedCaptureId;
        RowVersion = rowVersion;
    }

    internal static Device Create(
        NodeId nodeId,
        NonEmptyName displayName,
        ManagementEndpoint managementEndpoint,
        DeviceRole role)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(managementEndpoint);
        return new Device(
            DeviceId.New(),
            nodeId,
            displayName,
            managementEndpoint,
            role,
            enabled: true,
            lastSupportState: null,
            lastCompletedCaptureId: null,
            rowVersion: 1);
    }

    /// <summary>Rebuilds a device from persistence.</summary>
    public static Device Reconstitute(
        DeviceId id,
        NodeId nodeId,
        NonEmptyName displayName,
        ManagementEndpoint managementEndpoint,
        DeviceRole role,
        bool enabled,
        SupportState? lastSupportState,
        ulong rowVersion,
        Guid? lastCompletedCaptureId = null)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(managementEndpoint);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        return new Device(
            id,
            nodeId,
            displayName,
            managementEndpoint,
            role,
            enabled,
            lastSupportState,
            lastCompletedCaptureId,
            rowVersion);
    }

    public void Rename(NonEmptyName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        DisplayName = displayName;
        Touch();
    }

    public void Relocate(ManagementEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ManagementEndpoint = endpoint;
        Touch();
    }

    public void SetRole(DeviceRole role)
    {
        Role = role;
        Touch();
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Touch();
    }

    public void RecordSupportState(SupportState state)
    {
        LastSupportState = state;
        Touch();
    }

    /// <summary>Links the most recent completed capture (does not clear history).</summary>
    public void RecordCompletedCapture(Guid captureId)
    {
        if (captureId == Guid.Empty)
        {
            throw new DomainInvariantException("last_completed_capture_id cannot be empty.");
        }

        LastCompletedCaptureId = captureId;
        Touch();
    }

    private void Touch() => RowVersion++;
}
