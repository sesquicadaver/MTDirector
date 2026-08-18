using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>
/// Device belonging to a Node. Management endpoint is typed (<see cref="ManagementEndpoint"/>).
/// <see cref="ManagementState"/> is independent of <see cref="NodeStatus"/> (Onboarding Spec §4.2).
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

    public ManagementState ManagementState { get; private set; }

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
        ManagementState managementState,
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
        ManagementState = managementState;
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
            ManagementState.Unmanaged,
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
        ManagementState managementState,
        ulong rowVersion,
        Guid? lastCompletedCaptureId = null)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(managementEndpoint);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        if (!Enum.IsDefined(managementState))
        {
            throw new DomainInvariantException($"Unknown management state '{managementState}'.");
        }

        return new Device(
            id,
            nodeId,
            displayName,
            managementEndpoint,
            role,
            enabled,
            lastSupportState,
            managementState,
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

    /// <summary>Sets Device management state. Node-level MANAGED invariant is enforced on the Node aggregate.</summary>
    public void SetManagementState(ManagementState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new DomainInvariantException($"Unknown management state '{state}'.");
        }

        ManagementState = state;
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
