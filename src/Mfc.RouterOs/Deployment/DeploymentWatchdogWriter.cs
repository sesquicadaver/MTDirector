using System.Globalization;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Production rollback watchdog writer (Safe Deployment Spec §22–§27 / M4-05).
/// Uses <see cref="IRouterOsDeploymentSession"/> only — no free-form command API.
/// </summary>
public sealed class DeploymentWatchdogWriter : IDeploymentWatchdogPort
{
    public const string AnalyzerVersion = "mfc.routeros.deployment_watchdog.v1";

    private readonly IRouterOsDeploymentSession _session;

    public DeploymentWatchdogWriter(IRouterOsDeploymentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public async Task<DeploymentWatchdogExecutionResult> ArmWatchdogAsync(
        DeploymentWatchdogBundle bundle,
        DateTimeOffset routerClock,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        List<string> paths = [];
        try
        {
            TimeSpan remaining = remainingTtl ?? bundle.Ttl;
            if (remaining < DeploymentCodes.MinCommitMargin)
            {
                return Fail(DeploymentCodes.WatchdogDeadlineTooClose, "Remaining TTL is below the 30s commit margin.", paths);
            }

            if (bundle.Ttl < DeploymentCodes.MinRollbackTtl || bundle.Ttl > DeploymentCodes.MaxRollbackTtl)
            {
                return Fail(DeploymentCodes.RollbackTtlOutOfRange, "Rollback TTL is outside 60–600s.", paths);
            }

            if (bundle.ScriptAttributes.Any(static a =>
                    a.Key == "dont-require-permissions"
                    && !string.Equals(a.Value, "no", StringComparison.Ordinal)))
            {
                return Fail(DeploymentCodes.WatchdogScriptInvalid, "dont-require-permissions=yes is forbidden.", paths);
            }

            ActualManagedState before = await _session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
            if (NameExists(before, bundle.ScriptName, bundle.DeadlineSchedulerName, bundle.StartupSchedulerName))
            {
                return Fail(DeploymentCodes.WatchdogScriptCollision, "Watchdog resource name is already occupied.", paths);
            }

            DeploymentWriteExecutionResult script = await _session.AddRollbackScriptAsync(
                new RollbackScriptWrite(bundle.ScriptName, bundle.ScriptSource, bundle.ScriptSourceHash),
                cancellationToken).ConfigureAwait(false);
            paths.Add(script.Path);
            if (!script.Succeeded)
            {
                return Fail(DeploymentCodes.WatchdogArmFailed, script.Error ?? "Script add failed.", paths);
            }

            Hash256 observed = DeploymentWatchdogScript.HashSource(
                script.ReadBack.GetValueOrDefault("source") ?? bundle.ScriptSource);
            if (!observed.Equals(bundle.ScriptSourceHash))
            {
                return Fail(DeploymentCodes.WatchdogScriptInvalid, "Watchdog script source hash mismatch.", paths, observed);
            }

            DeploymentWriteExecutionResult startup = await _session.AddRollbackSchedulerAsync(
                new RollbackSchedulerWrite(
                    bundle.StartupSchedulerName,
                    bundle.ScriptName,
                    startTime: "startup",
                    interval: "0s"),
                cancellationToken).ConfigureAwait(false);
            paths.Add(startup.Path);
            if (!startup.Succeeded)
            {
                return Fail(DeploymentCodes.WatchdogArmFailed, startup.Error ?? "Startup scheduler add failed.", paths, observed);
            }

            DateTimeOffset deadline = routerClock + bundle.Ttl;
            DeploymentWriteExecutionResult end = await _session.AddRollbackSchedulerAsync(
                new RollbackSchedulerWrite(
                    bundle.DeadlineSchedulerName,
                    bundle.ScriptName,
                    startTime: FormatStartTime(deadline),
                    startDate: FormatStartDate(deadline),
                    interval: "0s"),
                cancellationToken).ConfigureAwait(false);
            paths.Add(end.Path);
            if (!end.Succeeded)
            {
                return Fail(DeploymentCodes.WatchdogArmFailed, end.Error ?? "Deadline scheduler add failed.", paths, observed);
            }

            ActualManagedState after = await _session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
            if (!SchedulerArmed(after, bundle.StartupSchedulerName, bundle.ScriptName, requireStartup: true)
                || !SchedulerArmed(after, bundle.DeadlineSchedulerName, bundle.ScriptName, requireStartup: false))
            {
                return Fail(DeploymentCodes.WatchdogArmFailed, "Watchdog scheduler read-back mismatch.", paths, observed);
            }

            return Ok(paths, observed);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(DeploymentCodes.WatchdogArmFailed, ex.Message, paths);
        }
    }

    public async Task<DeploymentWatchdogExecutionResult> DisarmWatchdogAsync(
        DeploymentWatchdogBundle bundle,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        List<string> paths = [];
        try
        {
            TimeSpan remaining = remainingTtl ?? bundle.Ttl;
            if (remaining < DeploymentCodes.MinCommitMargin)
            {
                return Fail(DeploymentCodes.WatchdogDeadlineTooClose, "Remaining TTL is below the 30s commit margin.", paths);
            }

            foreach (string name in new[] { bundle.DeadlineSchedulerName, bundle.StartupSchedulerName })
            {
                RouterOsItemId? id = await FindSchedulerIdAsync(name, cancellationToken).ConfigureAwait(false);
                if (id is null)
                {
                    return Fail(DeploymentCodes.WatchdogDisableFailed, $"Watchdog scheduler '{name}' was not found for disarm.", paths);
                }

                DeploymentWriteExecutionResult disabled = await _session.DisableRollbackSchedulerAsync(id.Value, cancellationToken)
                    .ConfigureAwait(false);
                paths.Add(disabled.Path);
                if (!disabled.Succeeded
                    || !Yes(disabled.ReadBack.GetValueOrDefault("disabled")))
                {
                    return Fail(
                        DeploymentCodes.WatchdogDisableFailed,
                        disabled.Error ?? $"Watchdog scheduler '{name}' was not disabled.",
                        paths);
                }
            }

            return Ok(paths, bundle.ScriptSourceHash);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(DeploymentCodes.WatchdogDisableFailed, ex.Message, paths);
        }
    }

    public async Task<DeploymentWatchdogExecutionResult> CleanupWatchdogAsync(
        DeploymentOperationId deploymentId,
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        string token = DeploymentWatchdogNames.Token(deploymentId, deviceId);
        string[] schedulers =
        [
            DeploymentWatchdogNames.DeadlineScheduler(token),
            DeploymentWatchdogNames.StartupScheduler(token),
        ];
        string script = DeploymentWatchdogNames.RollbackScript(token);
        List<string> paths = [];
        try
        {
            foreach (string name in schedulers)
            {
                RouterOsItemId? id = await FindSchedulerIdAsync(name, cancellationToken).ConfigureAwait(false);
                if (id is null)
                {
                    continue;
                }

                ActualManagedState state = await _session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
                IReadOnlyDictionary<string, string>? row = FindByName(state.Schedulers, name);
                if (row is not null && !Yes(row.GetValueOrDefault("disabled")))
                {
                    DeploymentWriteExecutionResult disabled = await _session.DisableRollbackSchedulerAsync(id.Value, cancellationToken)
                        .ConfigureAwait(false);
                    paths.Add(disabled.Path);
                    if (!disabled.Succeeded)
                    {
                        return Fail(DeploymentCodes.WatchdogCleanupIncomplete, disabled.Error ?? "Disable before remove failed.", paths);
                    }
                }

                DeploymentWriteExecutionResult removed = await _session.RemoveRollbackSchedulerAsync(id.Value, cancellationToken)
                    .ConfigureAwait(false);
                paths.Add(removed.Path);
                if (!removed.Succeeded && removed.Error is not null
                    && !removed.Error.Contains("Expected exactly one", StringComparison.Ordinal))
                {
                    return Fail(DeploymentCodes.WatchdogCleanupIncomplete, removed.Error, paths);
                }
            }

            RouterOsItemId? scriptId = await FindScriptIdAsync(script, cancellationToken).ConfigureAwait(false);
            if (scriptId is not null)
            {
                DeploymentWriteExecutionResult removed = await _session.RemoveRollbackScriptAsync(scriptId.Value, cancellationToken)
                    .ConfigureAwait(false);
                paths.Add(removed.Path);
                if (!removed.Succeeded && removed.Error is not null
                    && !removed.Error.Contains("Expected exactly one", StringComparison.Ordinal))
                {
                    return Fail(DeploymentCodes.WatchdogCleanupIncomplete, removed.Error, paths);
                }
            }

            ActualManagedState after = await _session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
            if (NameExists(after, script, schedulers[0], schedulers[1]))
            {
                return Fail(DeploymentCodes.WatchdogCleanupIncomplete, "Watchdog resources remained after cleanup.", paths);
            }

            return Ok(paths);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(DeploymentCodes.WatchdogCleanupIncomplete, ex.Message, paths);
        }
    }

    private async Task<RouterOsItemId?> FindSchedulerIdAsync(string name, CancellationToken cancellationToken)
    {
        ActualManagedState state = await _session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string>? row = FindByName(state.Schedulers, name);
        return row is not null
               && row.TryGetValue(".id", out string? id)
               && !string.IsNullOrWhiteSpace(id)
            ? RouterOsItemId.Create(id)
            : null;
    }

    private async Task<RouterOsItemId?> FindScriptIdAsync(string name, CancellationToken cancellationToken)
    {
        ActualManagedState state = await _session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string>? row = FindByName(state.Scripts, name);
        return row is not null
               && row.TryGetValue(".id", out string? id)
               && !string.IsNullOrWhiteSpace(id)
            ? RouterOsItemId.Create(id)
            : null;
    }

    private static bool NameExists(ActualManagedState state, params string[] names)
    {
        HashSet<string> wanted = new(names, StringComparer.Ordinal);
        return state.Scripts.Any(r => wanted.Contains(r.GetValueOrDefault("name") ?? string.Empty))
               || state.Schedulers.Any(r => wanted.Contains(r.GetValueOrDefault("name") ?? string.Empty));
    }

    private static bool SchedulerArmed(
        ActualManagedState state,
        string name,
        string scriptName,
        bool requireStartup)
    {
        IReadOnlyDictionary<string, string>? row = FindByName(state.Schedulers, name);
        if (row is null)
        {
            return false;
        }

        if (!string.Equals(row.GetValueOrDefault("on-event"), scriptName, StringComparison.Ordinal)
            || Yes(row.GetValueOrDefault("disabled")))
        {
            return false;
        }

        return !requireStartup
               || string.Equals(row.GetValueOrDefault("start-time"), "startup", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string>? FindByName(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string name)
        => rows.FirstOrDefault(r => string.Equals(r.GetValueOrDefault("name"), name, StringComparison.Ordinal));

    private static string FormatStartDate(DateTimeOffset value)
        => value.UtcDateTime.ToString("MMM/dd/yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    private static string FormatStartTime(DateTimeOffset value)
        => value.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static bool Yes(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static DeploymentWatchdogExecutionResult Ok(IReadOnlyList<string> paths, Hash256? hash = null)
        => new()
        {
            Succeeded = true,
            Code = "OK",
            Paths = paths,
            ObservedSourceHash = hash,
        };

    private static DeploymentWatchdogExecutionResult Fail(
        string code,
        string error,
        IReadOnlyList<string> paths,
        Hash256? hash = null)
        => new()
        {
            Succeeded = false,
            Code = code,
            Paths = paths,
            ObservedSourceHash = hash,
            Error = error,
        };
}
