namespace Mfc.Domain.Deployment;

/// <summary>One live address-list row used by create-or-verify (Safe Deployment Spec §18).</summary>
public sealed class ActualAddressListEntry
{
    public ActualAddressListEntry(
        string listName,
        string address,
        bool dynamic = false,
        string? timeout = null,
        string? comment = null,
        bool disabled = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listName);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ListName = listName.Trim();
        Address = address.Trim();
        Dynamic = dynamic;
        Timeout = string.IsNullOrWhiteSpace(timeout) ? null : timeout.Trim();
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        Disabled = disabled;
    }

    public string ListName { get; }

    public string Address { get; }

    public bool Dynamic { get; }

    public string? Timeout { get; }

    public string? Comment { get; }

    public bool Disabled { get; }

    public bool IsDynamicOrTimed => Dynamic || Timeout is not null;
}

/// <summary>Create-or-verify decision for one content-addressed list (Spec §18).</summary>
public enum AddressListStagingAction : byte
{
    Reuse = 0,
    CreateAll = 1,
    AddMissing = 2,
    Collision = 3,
}

/// <summary>Pure planner result — no RouterOS I/O (Spec §18 / M4-03).</summary>
public sealed class AddressListStagingPlan
{
    public required bool Succeeded { get; init; }

    public required AddressListStagingAction Action { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    /// <summary>Addresses to add via allowlisted <c>/add</c> only (never set/remove).</summary>
    public required IReadOnlyList<string> MissingAddresses { get; init; }

    public static AddressListStagingPlan Reuse()
        => new()
        {
            Succeeded = true,
            Action = AddressListStagingAction.Reuse,
            MissingAddresses = [],
        };

    public static AddressListStagingPlan CreateAll(IReadOnlyList<string> addresses)
        => new()
        {
            Succeeded = true,
            Action = AddressListStagingAction.CreateAll,
            MissingAddresses = addresses,
        };

    public static AddressListStagingPlan AddMissing(IReadOnlyList<string> missing)
        => new()
        {
            Succeeded = true,
            Action = AddressListStagingAction.AddMissing,
            MissingAddresses = missing,
        };

    public static AddressListStagingPlan Fail(string code, string message)
        => new()
        {
            Succeeded = false,
            Action = AddressListStagingAction.Collision,
            Code = code,
            Message = message,
            MissingAddresses = [],
        };
}
