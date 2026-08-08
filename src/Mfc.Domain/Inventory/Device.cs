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

    public ulong RowVersion { get; private set; }

    private Device(
        DeviceId id,
        NodeId nodeId,
        NonEmptyName displayName,
        ManagementEndpoint managementEndpoint,
        DeviceRole role,
        bool enabled,
        SupportState? lastSupportState,
        ulong rowVersion)
    {
        Id = id;
        NodeId = nodeId;
        DisplayName = displayName;
        ManagementEndpoint = managementEndpoint;
        Role = role;
        Enabled = enabled;
        LastSupportState = lastSupportState;
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
            rowVersion: 1);
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

    private void Touch() => RowVersion++;
}
