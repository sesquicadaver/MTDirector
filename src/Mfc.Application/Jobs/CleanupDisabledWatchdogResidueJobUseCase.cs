using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Domain;
using Mfc.Domain.Drift;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Jobs;

/// <summary>Batch result for disabled watchdog residue cleanup (M6-03).</summary>
public sealed class CleanupDisabledWatchdogResidueJobResult
{
    public required IReadOnlyList<string> RemovedNames { get; init; }

    public required IReadOnlyList<string> RejectedNames { get; init; }
}

/// <summary>
/// Removes only temporary disabled watchdog resources via <see cref="IWatchdogResidueCleanupPort"/>.
/// Fail-closed against firewall artifacts, snapshots, and audit.
/// </summary>
public sealed class CleanupDisabledWatchdogResidueJobUseCase
{
    private readonly IWatchdogResidueCleanupPort _cleanup;
    private readonly IDeviceStore _devices;

    public CleanupDisabledWatchdogResidueJobUseCase(IWatchdogResidueCleanupPort cleanup, IDeviceStore devices)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(devices);
        _cleanup = cleanup;
        _devices = devices;
    }

    public async Task<ApplicationResult<CleanupDisabledWatchdogResidueJobResult>> ExecuteAsync(
        Guid deviceId,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateNames);

        List<string> allowed = [];
        List<string> rejected = [];
        foreach (string name in candidateNames)
        {
            if (WatchdogResidueCleanupPolicy.IsAllowedTemporaryWatchdogResource(name)
                && !WatchdogResidueCleanupPolicy.IsForbiddenCleanupTarget(name))
            {
                allowed.Add(name.Trim());
            }
            else
            {
                rejected.Add(name);
            }
        }

        if (allowed.Count == 0)
        {
            return ApplicationResults.Ok(new CleanupDisabledWatchdogResidueJobResult
            {
                RemovedNames = [],
                RejectedNames = rejected,
            });
        }

        DeviceId id = new(deviceId);
        if (await _devices.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Device '{deviceId}' not found."));
        }

        try
        {
            foreach (string name in allowed)
            {
                WatchdogResidueCleanupPolicy.EnsureAllowed(name);
            }

            WatchdogResidueCleanupResult cleaned = await _cleanup
                .RemoveDisabledTemporaryWatchdogResourcesAsync(id, allowed, cancellationToken)
                .ConfigureAwait(false);
            if (!cleaned.Succeeded)
            {
                return ApplicationResults.Fail(
                    ApplicationError.Failed(cleaned.ErrorCode ?? "watchdog_residue_cleanup_failed"));
            }

            return ApplicationResults.Ok(new CleanupDisabledWatchdogResidueJobResult
            {
                RemovedNames = cleaned.RemovedNames,
                RejectedNames = rejected,
            });
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }
}
