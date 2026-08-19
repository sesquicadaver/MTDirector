using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Existing system script/scheduler names observed on a device (M5-06 collision input).</summary>
public sealed class OnboardingSystemNameFacts
{
    public required IReadOnlyList<string> ScriptNames { get; init; }

    public required IReadOnlyList<string> SchedulerNames { get; init; }
}

/// <summary>One scheduler-proof or watchdog planning finding.</summary>
public sealed class OnboardingWatchdogFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Target { get; init; }
}

/// <summary>Planned watchdog bundle for one Device (Onboarding Spec §32–§36).</summary>
public sealed class OnboardingWatchdogBundle
{
    public required string Token { get; init; }

    public required string ScriptName { get; init; }

    public required string DeadlineSchedulerName { get; init; }

    public required string StartupSchedulerName { get; init; }

    public required string ScriptSource { get; init; }

    public required Hash256 ScriptSourceHash { get; init; }

    public required TimeSpan Ttl { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> ScriptAttributes { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> DeadlineAttributes { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> StartupAttributes { get; init; }
}

/// <summary>Planned one-shot scheduler proof (Onboarding Spec §12).</summary>
public sealed class SchedulerProofPlan
{
    public required string Token { get; init; }

    public required string ScriptName { get; init; }

    public required string SchedulerName { get; init; }

    public required string ScriptSource { get; init; }

    public required Hash256 ScriptSourceHash { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> ScriptAttributes { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> SchedulerAttributes { get; init; }
}

/// <summary>Outcome of <see cref="OnboardingWatchdogPlanner"/>.</summary>
public sealed class OnboardingWatchdogPlanResult
{
    public required IReadOnlyList<OnboardingWatchdogFinding> Findings { get; init; }

    public SchedulerProofPlan? Proof { get; init; }

    public OnboardingWatchdogBundle? Watchdog { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != OnboardingCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == OnboardingCodes.SeverityBlocker);
}

/// <summary>
/// Plans scheduler capability proof and onboarding watchdog resources (M5-06).
/// Does not execute RouterOS commands.
/// </summary>
public static class OnboardingWatchdogPlanner
{
    public const string AnalyzerVersion = "mfc.onboarding.watchdog.v1";

    public static OnboardingWatchdogPlanResult PlanProof(
        DeviceId deviceId,
        OnboardingSystemNameFacts names)
    {
        ArgumentNullException.ThrowIfNull(names);
        string token = OnboardingWatchdogNames.Token(new OnboardingOperationId(Guid.Empty), deviceId);
        return PlanProofWithToken(token, names);
    }

    public static OnboardingWatchdogPlanResult PlanProofWithToken(string token, OnboardingSystemNameFacts names)
    {
        ArgumentNullException.ThrowIfNull(names);
        List<OnboardingWatchdogFinding> findings = [];
        string script = OnboardingWatchdogNames.CapabilityScript(token);
        string scheduler = OnboardingWatchdogNames.CapabilityScheduler(token);
        CheckCollision(names, [script, scheduler], findings);
        CheckOccupiedPrefix(names, findings);
        if (findings.Count > 0)
        {
            return Finish(findings, null, null);
        }

        KeyValuePair<string, string>[] scriptAttrs =
        [
            new("name", script),
            new("source", SchedulerCapabilityProof.NoOpSource),
            new("policy", SchedulerCapabilityProof.Policy),
            new("dont-require-permissions", SchedulerCapabilityProof.DontRequirePermissions),
        ];
        KeyValuePair<string, string>[] schedAttrs =
        [
            new("name", scheduler),
            new("on-event", script),
            new("interval", "0s"),
            new("policy", SchedulerCapabilityProof.Policy),
            new("disabled", "no"),
        ];
        RejectDontRequireYes(scriptAttrs, findings);
        RejectDontRequireYes(schedAttrs, findings);
        if (findings.Count > 0)
        {
            return Finish(findings, null, null);
        }

        return Finish(
            findings,
            new SchedulerProofPlan
            {
                Token = token,
                ScriptName = script,
                SchedulerName = scheduler,
                ScriptSource = SchedulerCapabilityProof.NoOpSource,
                ScriptSourceHash = SchedulerCapabilityProof.SourceHash,
                ScriptAttributes = scriptAttrs,
                SchedulerAttributes = schedAttrs,
            },
            null);
    }

    public static OnboardingWatchdogPlanResult PlanWatchdog(
        OnboardingOperationId operationId,
        DeviceOnboardingPlan devicePlan,
        OnboardingSystemNameFacts names)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(names);
        List<OnboardingWatchdogFinding> findings = [];
        if (devicePlan.WatchdogTtl < OnboardingCodes.MinWatchdogTtl
            || devicePlan.WatchdogTtl > OnboardingCodes.MaxWatchdogTtl)
        {
            findings.Add(Blocker(
                OnboardingCodes.WatchdogTtlOutOfRange,
                "Watchdog TTL is outside 60–600s.",
                "ttl"));
        }

        if (devicePlan.WatchdogTtl < OnboardingCodes.MinCommitMargin)
        {
            findings.Add(Blocker(
                OnboardingCodes.OnboardingWatchdogDeadlineTooClose,
                "Watchdog TTL is below the 30s commit margin.",
                "ttl"));
        }

        string token = OnboardingWatchdogNames.Token(operationId, devicePlan.DeviceId);
        string scriptName = OnboardingWatchdogNames.RollbackScript(token);
        string deadline = OnboardingWatchdogNames.DeadlineScheduler(token);
        string startup = OnboardingWatchdogNames.StartupScheduler(token);
        CheckCollision(names, [scriptName, deadline, startup], findings);
        CheckOccupiedPrefix(names, findings);

        string source = OnboardingWatchdogScript.Render(devicePlan.RequiredAnchorSet);
        if (ContainsUserText(source))
        {
            findings.Add(Blocker(
                OnboardingCodes.OnboardingWatchdogInvalid,
                "Watchdog script source contains forbidden user text.",
                "source"));
        }

        KeyValuePair<string, string>[] scriptAttrs =
        [
            new("name", scriptName),
            new("source", source),
            new("policy", OnboardingWatchdogScript.Policy),
            new("dont-require-permissions", OnboardingWatchdogScript.DontRequirePermissions),
        ];
        KeyValuePair<string, string>[] deadlineAttrs =
        [
            new("name", deadline),
            new("on-event", scriptName),
            new("interval", "0s"),
            new("policy", OnboardingWatchdogScript.Policy),
            new("disabled", "no"),
        ];
        KeyValuePair<string, string>[] startupAttrs =
        [
            new("name", startup),
            new("on-event", scriptName),
            new("start-time", "startup"),
            new("interval", "0s"),
            new("policy", OnboardingWatchdogScript.Policy),
            new("disabled", "no"),
        ];
        RejectDontRequireYes(scriptAttrs, findings);
        RejectDontRequireYes(deadlineAttrs, findings);
        RejectDontRequireYes(startupAttrs, findings);

        if (findings.Count > 0)
        {
            return Finish(findings, null, null);
        }

        return Finish(
            findings,
            null,
            new OnboardingWatchdogBundle
            {
                Token = token,
                ScriptName = scriptName,
                DeadlineSchedulerName = deadline,
                StartupSchedulerName = startup,
                ScriptSource = source,
                ScriptSourceHash = OnboardingWatchdogScript.HashSource(source),
                Ttl = devicePlan.WatchdogTtl,
                ScriptAttributes = scriptAttrs,
                DeadlineAttributes = deadlineAttrs,
                StartupAttributes = startupAttrs,
            });
    }

    private static void CheckOccupiedPrefix(OnboardingSystemNameFacts names, List<OnboardingWatchdogFinding> findings)
    {
        foreach (string name in names.ScriptNames.Concat(names.SchedulerNames))
        {
            if (OnboardingWatchdogNames.IsOnboardingWatchdogName(name)
                || OnboardingWatchdogNames.IsCapabilityProofName(name))
            {
                findings.Add(Blocker(
                    OnboardingCodes.OnboardingWatchdogCollision,
                    $"Existing onboarding watchdog name '{name}' blocks the operation.",
                    name));
                findings.Add(Blocker(
                    OnboardingCodes.MfcNamespaceCollision,
                    $"MFC namespace collision on '{name}'.",
                    name));
            }
        }
    }

    private static void CheckCollision(
        OnboardingSystemNameFacts names,
        IReadOnlyList<string> planned,
        List<OnboardingWatchdogFinding> findings)
    {
        HashSet<string> existing = new(names.ScriptNames.Concat(names.SchedulerNames), StringComparer.Ordinal);
        foreach (string name in planned)
        {
            if (existing.Contains(name))
            {
                findings.Add(Blocker(
                    OnboardingCodes.OnboardingWatchdogCollision,
                    $"Watchdog/proof name '{name}' is already occupied.",
                    name));
            }
        }
    }

    private static void RejectDontRequireYes(
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        List<OnboardingWatchdogFinding> findings)
    {
        foreach (KeyValuePair<string, string> pair in attributes)
        {
            if (pair.Key == "dont-require-permissions"
                && !string.Equals(pair.Value, "no", StringComparison.Ordinal))
            {
                findings.Add(Blocker(
                    OnboardingCodes.OnboardingWatchdogInvalid,
                    "dont-require-permissions=yes is forbidden.",
                    "permissions"));
            }
        }
    }

    private static bool ContainsUserText(string source)
    {
        string[] forbidden = ["ticket", "username", "password", "http://", "ftp://", "/file"];
        return forbidden.Any(f => source.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    private static OnboardingWatchdogFinding Blocker(string code, string message, string? target)
        => new()
        {
            Code = code,
            Severity = OnboardingCodes.SeverityBlocker,
            Message = message,
            Target = target,
        };

    private static OnboardingWatchdogPlanResult Finish(
        List<OnboardingWatchdogFinding> findings,
        SchedulerProofPlan? proof,
        OnboardingWatchdogBundle? watchdog)
    {
        IReadOnlyList<OnboardingWatchdogFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Message, f.Target))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();
        return new OnboardingWatchdogPlanResult
        {
            Findings = ordered,
            Proof = proof,
            Watchdog = watchdog,
        };
    }
}
