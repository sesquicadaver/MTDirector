using System.Collections.Immutable;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Address-list create-or-verify planner (Safe Deployment Spec §18 / Compiler Spec §26 / M4-03).
/// Pure Domain: no RouterOS I/O, no set/remove, no blind-add advice.
/// </summary>
public static class AddressListCreateOrVerify
{
    /// <summary>
    /// Plans staging for one content-addressed list against the actual rows of that list only.
    /// Caller must re-read actual state before every planning attempt (Spec §18 / §51).
    /// </summary>
    public static AddressListStagingPlan Plan(
        AddressListArtifactDraft desired,
        IReadOnlyList<ActualAddressListEntry> actualRows,
        AddressListCompileLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(actualRows);
        AddressListCompileLimits effectiveLimits = limits ?? AddressListCompileLimits.LayoutV1;
        effectiveLimits.EnsureWithinLayoutV1();

        if (string.IsNullOrWhiteSpace(desired.Name))
        {
            return AddressListStagingPlan.Fail(
                DeploymentCodes.StagingRuleInvalid,
                "Desired address-list name is required.");
        }

        if (!Enum.IsDefined(desired.Family))
        {
            return AddressListStagingPlan.Fail(
                DeploymentCodes.StagingRuleInvalid,
                $"Unknown address family '{desired.Family}'.");
        }

        ImmutableArray<AddressListEntryArtifact> desiredEntries = desired.Entries
            .OrderBy(static e => e.Address, StringComparer.Ordinal)
            .ToImmutableArray();
        Hash256 desiredHash = RouterOsFilterArtifactIdentity.HashAddressListContent(desired.Family, desiredEntries);
        string expectedName = ManagedChainNamespace.AddressListName(
            desired.Family,
            desiredHash.ToString()[..RouterOsFilterArtifactIdentity.ArtifactIdHexLength]);
        if (!string.Equals(desired.Name.Trim(), expectedName, StringComparison.Ordinal))
        {
            return AddressListStagingPlan.Fail(
                DeploymentCodes.StagingArtifactHashMismatch,
                $"Desired list name '{desired.Name}' does not match content-addressed name '{expectedName}'.");
        }

        if (desiredEntries.Length > effectiveLimits.MaxEntriesPerFamily)
        {
            return AddressListStagingPlan.Fail(
                DeploymentCodes.StagingLimitExceeded,
                $"Desired entries exceed MaxEntriesPerFamily ({effectiveLimits.MaxEntriesPerFamily}).");
        }

        List<ActualAddressListEntry> scoped = actualRows
            .Where(r => string.Equals(r.ListName, desired.Name.Trim(), StringComparison.Ordinal))
            .ToList();

        foreach (ActualAddressListEntry row in scoped)
        {
            if (row.IsDynamicOrTimed)
            {
                return AddressListStagingPlan.Fail(
                    DeploymentCodes.StagingRuleInvalid,
                    $"Dynamic or timed entry '{row.Address}' blocks staging of generated list '{desired.Name}'.");
            }

            if (IsUnmanaged(row))
            {
                return AddressListStagingPlan.Fail(
                    DeploymentCodes.StagingResourceCollision,
                    $"Unmanaged entry '{row.Address}' in generated list '{desired.Name}' blocks staging.");
            }

            if (row.Disabled)
            {
                return AddressListStagingPlan.Fail(
                    DeploymentCodes.StagingResourceCollision,
                    $"Disabled entry '{row.Address}' diverges from desired static list '{desired.Name}'.");
            }
        }

        HashSet<string> desiredAddresses = desiredEntries
            .Select(static e => e.Address)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualAddresses = [];
        foreach (ActualAddressListEntry row in scoped)
        {
            if (!actualAddresses.Add(row.Address))
            {
                return AddressListStagingPlan.Fail(
                    DeploymentCodes.StagingResourceCollision,
                    $"Duplicate address '{row.Address}' in list '{desired.Name}'.");
            }

            if (!desiredAddresses.Contains(row.Address))
            {
                return AddressListStagingPlan.Fail(
                    DeploymentCodes.StagingResourceCollision,
                    $"Extra address '{row.Address}' in list '{desired.Name}' (no in-place edit).");
            }
        }

        if (actualAddresses.Count == 0)
        {
            return AddressListStagingPlan.CreateAll(desiredAddresses.OrderBy(static a => a, StringComparer.Ordinal).ToArray());
        }

        if (actualAddresses.SetEquals(desiredAddresses))
        {
            return AddressListStagingPlan.Reuse();
        }

        // Proper subset: every actual address is desired; some desired are missing.
        if (actualAddresses.IsSubsetOf(desiredAddresses))
        {
            string[] missing = desiredAddresses
                .Where(a => !actualAddresses.Contains(a))
                .OrderBy(static a => a, StringComparer.Ordinal)
                .ToArray();
            return AddressListStagingPlan.AddMissing(missing);
        }

        return AddressListStagingPlan.Fail(
            DeploymentCodes.StagingResourceCollision,
            $"Address-list '{desired.Name}' diverges from desired content.");
    }

    /// <summary>
    /// Verifies unordered content hash of actual static addresses against the desired list.
    /// </summary>
    public static bool TryVerifyContentHash(
        AddressListArtifactDraft desired,
        IReadOnlyList<ActualAddressListEntry> actualRows,
        out Hash256 observedHash,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(actualRows);
        List<AddressListEntryArtifact> entries = [];
        foreach (ActualAddressListEntry row in actualRows
                     .Where(r => string.Equals(r.ListName, desired.Name.Trim(), StringComparison.Ordinal))
                     .OrderBy(static r => r.Address, StringComparer.Ordinal))
        {
            if (row.IsDynamicOrTimed || IsUnmanaged(row) || row.Disabled)
            {
                observedHash = Hash256.Create(new byte[Hash256.Size]);
                error = $"Cannot verify hash while list '{desired.Name}' still has invalid rows.";
                return false;
            }

            entries.Add(AddressListEntryArtifact.Create(row.Address));
        }

        observedHash = RouterOsFilterArtifactIdentity.HashAddressListContent(desired.Family, entries);
        Hash256 desiredHash = RouterOsFilterArtifactIdentity.HashAddressListContent(
            desired.Family,
            desired.Entries.OrderBy(static e => e.Address, StringComparer.Ordinal).ToArray());
        if (!observedHash.Equals(desiredHash))
        {
            error = DeploymentCodes.StagingArtifactHashMismatch;
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Foreign comments in a generated list are unmanaged (Spec §18 step 2 / AC#4).
    /// Empty comments are allowed (compiler entries are address-only).
    /// </summary>
    public static bool IsUnmanaged(ActualAddressListEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Comment is null)
        {
            return false;
        }

        return !row.Comment.StartsWith(ActualFilterMarker.MfcPrefix, StringComparison.Ordinal)
               && !row.Comment.StartsWith(ActualFilterMarker.FwcPrefix, StringComparison.Ordinal);
    }
}
