using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>Outcome of staging one content-addressed address list (Safe Deployment Spec §18 / M4-03).</summary>
public sealed class AddressListStagingResult
{
    public required bool Succeeded { get; init; }

    public required AddressListStagingAction Action { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public Hash256? ObservedContentHash { get; init; }

    public required int AddedCount { get; init; }

    public required int ReadBeforeWriteCount { get; init; }
}

/// <summary>
/// Stages immutable content-addressed address lists via create-or-verify (M4-03).
/// Always reads actual state before deciding adds; never uses address-list set/remove.
/// </summary>
public static class StageAddressListUseCase
{
    /// <summary>
    /// Stages <paramref name="desired"/> on the open deployment session.
    /// Blind <c>/add</c> retry without a preceding read is impossible: every attempt starts with print.
    /// </summary>
    public static async Task<AddressListStagingResult> ExecuteAsync(
        AddressListArtifactDraft desired,
        IRouterOsDeploymentSession session,
        AddressListCompileLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(session);

        int readCount = 0;
        ActualManagedState state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        readCount++;
        IReadOnlyList<ActualAddressListEntry> actual = Map(desired.Family, state);

        AddressListStagingPlan plan = AddressListCreateOrVerify.Plan(desired, actual, limits);
        if (!plan.Succeeded)
        {
            return Fail(plan, readCount);
        }

        if (plan.Action == AddressListStagingAction.Reuse)
        {
            if (!AddressListCreateOrVerify.TryVerifyContentHash(desired, actual, out Hash256 hash, out string? verifyError))
            {
                return new AddressListStagingResult
                {
                    Succeeded = false,
                    Action = AddressListStagingAction.Collision,
                    Code = DeploymentCodes.StagingArtifactHashMismatch,
                    Message = verifyError,
                    ObservedContentHash = hash,
                    AddedCount = 0,
                    ReadBeforeWriteCount = readCount,
                };
            }

            return new AddressListStagingResult
            {
                Succeeded = true,
                Action = AddressListStagingAction.Reuse,
                ObservedContentHash = hash,
                AddedCount = 0,
                ReadBeforeWriteCount = readCount,
            };
        }

        int added = 0;
        foreach (string address in plan.MissingAddresses)
        {
            // Spec §51 / AC#5–#6: never blind-add; re-read actual before every mutation batch item
            // after the first so reconnect/retry paths see live state.
            if (added > 0)
            {
                state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
                readCount++;
                actual = Map(desired.Family, state);
                AddressListStagingPlan refreshed = AddressListCreateOrVerify.Plan(desired, actual, limits);
                if (!refreshed.Succeeded)
                {
                    return Fail(refreshed, readCount, added);
                }

                if (refreshed.Action == AddressListStagingAction.Reuse)
                {
                    break;
                }

                if (!refreshed.MissingAddresses.Contains(address, StringComparer.Ordinal))
                {
                    // Already present after reconnect — skip blind duplicate add.
                    continue;
                }
            }

            DeploymentWriteExecutionResult write = await session.AddAddressListEntryAsync(
                new AddressListEntryWrite(desired.Family, desired.Name, address),
                cancellationToken).ConfigureAwait(false);
            if (!write.Succeeded)
            {
                return new AddressListStagingResult
                {
                    Succeeded = false,
                    Action = AddressListStagingAction.Collision,
                    Code = DeploymentCodes.StagingResourceCollision,
                    Message = write.Error ?? "Address-list add failed.",
                    AddedCount = added,
                    ReadBeforeWriteCount = readCount,
                };
            }

            added++;
        }

        state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        readCount++;
        actual = Map(desired.Family, state);
        if (!AddressListCreateOrVerify.TryVerifyContentHash(desired, actual, out Hash256 observed, out string? error))
        {
            return new AddressListStagingResult
            {
                Succeeded = false,
                Action = AddressListStagingAction.Collision,
                Code = DeploymentCodes.StagingArtifactHashMismatch,
                Message = error,
                ObservedContentHash = observed,
                AddedCount = added,
                ReadBeforeWriteCount = readCount,
            };
        }

        return new AddressListStagingResult
        {
            Succeeded = true,
            Action = plan.Action,
            ObservedContentHash = observed,
            AddedCount = added,
            ReadBeforeWriteCount = readCount,
        };
    }

    private static AddressListStagingResult Fail(AddressListStagingPlan plan, int readCount, int added = 0)
        => new()
        {
            Succeeded = false,
            Action = plan.Action,
            Code = plan.Code,
            Message = plan.Message,
            AddedCount = added,
            ReadBeforeWriteCount = readCount,
        };

    private static List<ActualAddressListEntry> Map(IpAddressFamily family, ActualManagedState state)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = family == IpAddressFamily.IPv4
            ? state.Ipv4AddressLists
            : state.Ipv6AddressLists;
        List<ActualAddressListEntry> mapped = new(rows.Count);
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            string? list = row.GetValueOrDefault("list");
            string? address = row.GetValueOrDefault("address");
            if (string.IsNullOrWhiteSpace(list) || string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            mapped.Add(new ActualAddressListEntry(
                list,
                address,
                dynamic: Yes(row.GetValueOrDefault("dynamic")),
                timeout: row.GetValueOrDefault("timeout"),
                comment: row.GetValueOrDefault("comment"),
                disabled: Yes(row.GetValueOrDefault("disabled"))));
        }

        return mapped;
    }

    private static bool Yes(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
