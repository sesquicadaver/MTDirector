namespace Mfc.Desktop.Services;

/// <summary>Lightweight Site/Node picker row for Desktop combo boxes (Contracts Guid only).</summary>
public sealed class InventoryPickerItem
{
    public InventoryPickerItem(Guid id, string label)
    {
        Id = id;
        Label = label ?? throw new ArgumentNullException(nameof(label));
    }

    public Guid Id { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
