using System.Globalization;
using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Closed scheduler-proof and onboarding watchdog writer (M5-06).
/// Paths come from <see cref="OnboardingWritePath"/> only. There is no free-form command method.
/// </summary>
public sealed class OnboardingWatchdogWriter : IOnboardingWatchdogPort
{
    public const string AnalyzerVersion = "mfc.routeros.onboarding_watchdog.v1";

    private readonly IOnboardingWriteChannel _channel;
    private readonly TimeProvider _time;

    public OnboardingWatchdogWriter(IOnboardingWriteChannel channel, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
        _time = time ?? TimeProvider.System;
    }

    public async Task<OnboardingWatchdogExecutionResult> ProveSchedulerAsync(
        SchedulerProofPlan plan,
        DateTimeOffset routerClock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        List<KeyValuePair<string, string>> sent = [];
        List<string> paths = [];
        try
        {
            OnboardingWatchdogExecutionResult? collision = await CollisionAsync(
                [plan.ScriptName, plan.SchedulerName],
                cancellationToken).ConfigureAwait(false);
            if (collision is not null)
            {
                return collision;
            }

            if (plan.ScriptAttributes.Any(static a =>
                    a.Key == "dont-require-permissions"
                    && !string.Equals(a.Value, "no", StringComparison.Ordinal)))
            {
                return Fail(OnboardingCodes.OnboardingWatchdogInvalid, "dont-require-permissions=yes is forbidden.", paths, sent);
            }

            await SendTrackedAsync(OnboardingWritePath.SystemScriptAdd, plan.ScriptAttributes, paths, sent, cancellationToken)
                .ConfigureAwait(false);
            Hash256 observed = await ReadSourceHashAsync(plan.ScriptName, cancellationToken).ConfigureAwait(false);
            if (!observed.Equals(plan.ScriptSourceHash))
            {
                return Fail(OnboardingCodes.OnboardingWatchdogInvalid, "Proof script source hash mismatch.", paths, sent, observed);
            }

            DateTimeOffset start = routerClock + SchedulerCapabilityProof.StartDelay;
            List<KeyValuePair<string, string>> schedulerAttrs =
            [
                .. plan.SchedulerAttributes,
                new("start-date", FormatStartDate(start)),
                new("start-time", FormatStartTime(start)),
            ];
            await SendTrackedAsync(OnboardingWritePath.SystemSchedulerAdd, schedulerAttrs, paths, sent, cancellationToken)
                .ConfigureAwait(false);

            int runCount = await WaitRunCountAsync(plan.SchedulerName, cancellationToken).ConfigureAwait(false);
            if (runCount != 1)
            {
                await TryCleanupProofAsync(plan, paths, sent, cancellationToken).ConfigureAwait(false);
                return Fail(
                    OnboardingCodes.SchedulerCapabilityTestFailed,
                    $"Scheduler run-count was {runCount}, expected 1.",
                    paths,
                    sent,
                    observed,
                    runCount);
            }

            await RemoveNamedAsync(OnboardingSystemSurface.Scheduler, OnboardingWritePath.SystemSchedulerRemove, plan.SchedulerName, paths, sent, cancellationToken)
                .ConfigureAwait(false);
            await RemoveNamedAsync(OnboardingSystemSurface.Script, OnboardingWritePath.SystemScriptRemove, plan.ScriptName, paths, sent, cancellationToken)
                .ConfigureAwait(false);
            if (await ExistsAsync(OnboardingSystemSurface.Scheduler, plan.SchedulerName, cancellationToken).ConfigureAwait(false)
                || await ExistsAsync(OnboardingSystemSurface.Script, plan.ScriptName, cancellationToken).ConfigureAwait(false))
            {
                return Fail(OnboardingCodes.OnboardingWatchdogCleanupIncomplete, "Proof resources remained after cleanup.", paths, sent, observed, runCount);
            }

            return Ok(paths, sent, observed, runCount);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(OnboardingCodes.SchedulerCapabilityTestFailed, ex.Message, paths, sent);
        }
    }

    public async Task<OnboardingWatchdogExecutionResult> ArmWatchdogAsync(
        OnboardingWatchdogBundle bundle,
        DateTimeOffset routerClock,
        TimeSpan? remainingTtl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        List<KeyValuePair<string, string>> sent = [];
        List<string> paths = [];
        try
        {
            TimeSpan remaining = remainingTtl ?? bundle.Ttl;
            if (remaining < OnboardingCodes.MinCommitMargin)
            {
                return Fail(
                    OnboardingCodes.OnboardingWatchdogDeadlineTooClose,
                    "Remaining watchdog TTL is below the 30s commit margin.",
                    paths,
                    sent);
            }

            if (bundle.Ttl < OnboardingCodes.MinWatchdogTtl || bundle.Ttl > OnboardingCodes.MaxWatchdogTtl)
            {
                return Fail(OnboardingCodes.WatchdogTtlOutOfRange, "Watchdog TTL is outside 60–600s.", paths, sent);
            }

            OnboardingWatchdogExecutionResult? collision = await CollisionAsync(
                [bundle.ScriptName, bundle.DeadlineSchedulerName, bundle.StartupSchedulerName],
                cancellationToken).ConfigureAwait(false);
            if (collision is not null)
            {
                return collision;
            }

            if (bundle.ScriptAttributes.Any(static a =>
                    a.Key == "dont-require-permissions"
                    && !string.Equals(a.Value, "no", StringComparison.Ordinal)))
            {
                return Fail(OnboardingCodes.OnboardingWatchdogInvalid, "dont-require-permissions=yes is forbidden.", paths, sent);
            }

            await SendTrackedAsync(OnboardingWritePath.SystemScriptAdd, bundle.ScriptAttributes, paths, sent, cancellationToken)
                .ConfigureAwait(false);
            Hash256 observed = await ReadSourceHashAsync(bundle.ScriptName, cancellationToken).ConfigureAwait(false);
            if (!observed.Equals(bundle.ScriptSourceHash))
            {
                return Fail(OnboardingCodes.OnboardingWatchdogInvalid, "Watchdog script source hash mismatch.", paths, sent, observed);
            }

            await SendTrackedAsync(OnboardingWritePath.SystemSchedulerAdd, bundle.StartupAttributes, paths, sent, cancellationToken)
                .ConfigureAwait(false);

            DateTimeOffset deadline = routerClock + bundle.Ttl;
            List<KeyValuePair<string, string>> deadlineAttrs =
            [
                .. bundle.DeadlineAttributes,
                new("start-date", FormatStartDate(deadline)),
                new("start-time", FormatStartTime(deadline)),
            ];
            await SendTrackedAsync(OnboardingWritePath.SystemSchedulerAdd, deadlineAttrs, paths, sent, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyDictionary<string, string>? script = await FindAsync(OnboardingSystemSurface.Script, bundle.ScriptName, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyDictionary<string, string>? startup = await FindAsync(OnboardingSystemSurface.Scheduler, bundle.StartupSchedulerName, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyDictionary<string, string>? end = await FindAsync(OnboardingSystemSurface.Scheduler, bundle.DeadlineSchedulerName, cancellationToken)
                .ConfigureAwait(false);
            if (script is null || startup is null || end is null)
            {
                return Fail(OnboardingCodes.OnboardingWatchdogArmFailed, "Watchdog read-back is missing a required resource.", paths, sent, observed);
            }

            if (!string.Equals(startup.GetValueOrDefault("on-event"), bundle.ScriptName, StringComparison.Ordinal)
                || !string.Equals(end.GetValueOrDefault("on-event"), bundle.ScriptName, StringComparison.Ordinal)
                || !string.Equals(startup.GetValueOrDefault("start-time"), "startup", StringComparison.Ordinal)
                || DisabledYes(startup.GetValueOrDefault("disabled"))
                || DisabledYes(end.GetValueOrDefault("disabled")))
            {
                return Fail(OnboardingCodes.OnboardingWatchdogArmFailed, "Watchdog scheduler policy/on-event/disabled mismatch.", paths, sent, observed);
            }

            return Ok(paths, sent, observed);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(OnboardingCodes.OnboardingWatchdogArmFailed, ex.Message, paths, sent);
        }
    }

    private async Task<OnboardingWatchdogExecutionResult?> CollisionAsync(
        IReadOnlyList<string> planned,
        CancellationToken cancellationToken)
    {
        HashSet<string> existing = new(StringComparer.Ordinal);
        foreach (IReadOnlyDictionary<string, string> row in (await _channel.PrintSystemAsync(OnboardingSystemSurface.Script, cancellationToken).ConfigureAwait(false))
                 .Concat(await _channel.PrintSystemAsync(OnboardingSystemSurface.Scheduler, cancellationToken).ConfigureAwait(false)))
        {
            if (row.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
            {
                existing.Add(name);
                if (OnboardingWatchdogNames.IsOnboardingWatchdogName(name)
                    || OnboardingWatchdogNames.IsCapabilityProofName(name))
                {
                    return Fail(
                        OnboardingCodes.OnboardingWatchdogCollision,
                        $"Existing onboarding name '{name}' blocks the operation.",
                        [],
                        []);
                }
            }
        }

        foreach (string name in planned)
        {
            if (existing.Contains(name))
            {
                return Fail(
                    OnboardingCodes.OnboardingWatchdogCollision,
                    $"Watchdog/proof name '{name}' is already occupied.",
                    [],
                    []);
            }
        }

        return null;
    }

    private async Task<Hash256> ReadSourceHashAsync(string scriptName, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? row = await FindAsync(OnboardingSystemSurface.Script, scriptName, cancellationToken)
            .ConfigureAwait(false);
        string source = row?.GetValueOrDefault("source") ?? throw new InvalidOperationException("Script source read-back is missing.");
        string normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        return OnboardingWatchdogScript.HashSource(normalized);
    }

    private async Task<int> WaitRunCountAsync(string schedulerName, CancellationToken cancellationToken)
    {
        long started = _time.GetTimestamp();
        while (true)
        {
            IReadOnlyDictionary<string, string>? row = await FindAsync(OnboardingSystemSurface.Scheduler, schedulerName, cancellationToken)
                .ConfigureAwait(false);
            if (row is not null
                && int.TryParse(row.GetValueOrDefault("run-count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                && count == 1)
            {
                return count;
            }

            if (_time.GetElapsedTime(started) >= OnboardingCodes.SchedulerProofTimeout)
            {
                return row is not null
                       && int.TryParse(row.GetValueOrDefault("run-count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int last)
                    ? last
                    : 0;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TryCleanupProofAsync(
        SchedulerProofPlan plan,
        List<string> paths,
        List<KeyValuePair<string, string>> sent,
        CancellationToken cancellationToken)
    {
        try
        {
            await RemoveNamedAsync(OnboardingSystemSurface.Scheduler, OnboardingWritePath.SystemSchedulerRemove, plan.SchedulerName, paths, sent, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Best-effort cleanup after a failed proof; the failure code stays SCHEDULER_CAPABILITY_TEST_FAILED.
        }

        try
        {
            await RemoveNamedAsync(OnboardingSystemSurface.Script, OnboardingWritePath.SystemScriptRemove, plan.ScriptName, paths, sent, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Same as scheduler cleanup: absence is acceptable after a failed proof.
        }
    }

    private async Task RemoveNamedAsync(
        OnboardingSystemSurface surface,
        OnboardingWritePath path,
        string name,
        List<string> paths,
        List<KeyValuePair<string, string>> sent,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? row = await FindAsync(surface, name, cancellationToken).ConfigureAwait(false);
        if (row is null || !row.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException($"Cannot remove '{name}' without a live .id from read-back.");
        }

        await SendTrackedAsync(path, [new(".id", itemId)], paths, sent, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendTrackedAsync(
        OnboardingWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        List<string> paths,
        List<KeyValuePair<string, string>> sent,
        CancellationToken cancellationToken)
    {
        if (attributes.Any(static a => a.Key.Contains("move", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Onboarding watchdog writer must not use move.");
        }

        paths.Add(OnboardingWritePaths.Fixed(path));
        sent.AddRange(attributes);
        await _channel.SendAsync(path, attributes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ExistsAsync(OnboardingSystemSurface surface, string name, CancellationToken cancellationToken)
        => await FindAsync(surface, name, cancellationToken).ConfigureAwait(false) is not null;

    private async Task<IReadOnlyDictionary<string, string>?> FindAsync(
        OnboardingSystemSurface surface,
        string name,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = await _channel.PrintSystemAsync(surface, cancellationToken)
            .ConfigureAwait(false);
        return rows.FirstOrDefault(r => string.Equals(r.GetValueOrDefault("name"), name, StringComparison.Ordinal));
    }

    private static bool DisabledYes(string? raw) => raw is "yes" or "true" or "1";

    private static string FormatStartTime(DateTimeOffset value)
        => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatStartDate(DateTimeOffset value)
        => value.ToString("MMM/dd/yyyy", CultureInfo.InvariantCulture).ToLowerInvariant();

    private static OnboardingWatchdogExecutionResult Ok(
        IReadOnlyList<string> paths,
        IReadOnlyList<KeyValuePair<string, string>> sent,
        Hash256? hash,
        int? runCount = null)
        => new()
        {
            Succeeded = true,
            Code = string.Empty,
            Paths = paths,
            SentAttributes = sent,
            ObservedSourceHash = hash,
            RunCount = runCount,
        };

    private static OnboardingWatchdogExecutionResult Fail(
        string code,
        string error,
        IReadOnlyList<string> paths,
        IReadOnlyList<KeyValuePair<string, string>> sent,
        Hash256? hash = null,
        int? runCount = null)
        => new()
        {
            Succeeded = false,
            Code = code,
            Paths = paths,
            SentAttributes = sent,
            ObservedSourceHash = hash,
            RunCount = runCount,
            Error = error,
        };
}
