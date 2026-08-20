using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;

namespace Mfc.Domain.Deployment;

/// <summary>Existing system script/scheduler names observed on a device (M4-05 collision input).</summary>
public sealed class DeploymentSystemNameFacts
{
    public required IReadOnlyList<string> ScriptNames { get; init; }

    public required IReadOnlyList<string> SchedulerNames { get; init; }
}

/// <summary>One deployment watchdog planning finding.</summary>
public sealed class DeploymentWatchdogFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Target { get; init; }
}

/// <summary>Planned production watchdog bundle for one Device (Safe Deployment Spec §22–§26).</summary>
public sealed class DeploymentWatchdogBundle
{
    public required string Token { get; init; }

    public required DeviceId DeviceId { get; init; }

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

/// <summary>Outcome of <see cref="DeploymentWatchdogPlanner"/>.</summary>
public sealed class DeploymentWatchdogPlanResult
{
    public required IReadOnlyList<DeploymentWatchdogFinding> Findings { get; init; }

    public DeploymentWatchdogBundle? Watchdog { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != DeploymentCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == DeploymentCodes.SeverityBlocker);
}

/// <summary>
/// Plans production rollback watchdog resources (M4-05). Does not execute RouterOS commands.
/// </summary>
public static class DeploymentWatchdogPlanner
{
    public const string AnalyzerVersion = "mfc.deployment.watchdog.v1";

    public static DeploymentWatchdogPlanResult PlanWatchdog(
        DeploymentOperationId deploymentId,
        DeviceDeploymentPlan devicePlan,
        DeploymentSystemNameFacts names)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(names);
        List<DeploymentWatchdogFinding> findings = [];
        if (devicePlan.RollbackTtl < DeploymentCodes.MinRollbackTtl
            || devicePlan.RollbackTtl > DeploymentCodes.MaxRollbackTtl)
        {
            findings.Add(Blocker(
                DeploymentCodes.RollbackTtlOutOfRange,
                "Rollback TTL is outside 60–600s.",
                "ttl"));
        }

        if (devicePlan.RollbackTtl < DeploymentCodes.MinCommitMargin)
        {
            findings.Add(Blocker(
                DeploymentCodes.WatchdogDeadlineTooClose,
                "Rollback TTL is below the 30s commit margin.",
                "ttl"));
        }

        string token = DeploymentWatchdogNames.Token(deploymentId, devicePlan.DeviceId);
        string scriptName = DeploymentWatchdogNames.RollbackScript(token);
        string deadline = DeploymentWatchdogNames.DeadlineScheduler(token);
        string startup = DeploymentWatchdogNames.StartupScheduler(token);
        CheckCollision(names, [scriptName, deadline, startup], findings);
        CheckOccupiedPrefix(names, findings);

        string source;
        try
        {
            source = DeploymentWatchdogScript.Render(
                devicePlan.OldAnchorTargets,
                devicePlan.NewAnchorTargets,
                devicePlan.AnchorRollbackOrder);
        }
        catch (DomainInvariantException ex)
        {
            findings.Add(Blocker(DeploymentCodes.WatchdogScriptInvalid, ex.Message, "source"));
            return Finish(findings, null);
        }

        if (ContainsUserText(source))
        {
            findings.Add(Blocker(
                DeploymentCodes.WatchdogScriptInvalid,
                "Watchdog script source contains forbidden user text.",
                "source"));
        }

        KeyValuePair<string, string>[] scriptAttrs =
        [
            new("name", scriptName),
            new("source", source),
            new("policy", DeploymentWatchdogScript.Policy),
            new("dont-require-permissions", DeploymentWatchdogScript.DontRequirePermissions),
        ];
        KeyValuePair<string, string>[] deadlineAttrs =
        [
            new("name", deadline),
            new("on-event", scriptName),
            new("interval", "0s"),
            new("policy", DeploymentWatchdogScript.Policy),
            new("disabled", "no"),
        ];
        KeyValuePair<string, string>[] startupAttrs =
        [
            new("name", startup),
            new("on-event", scriptName),
            new("start-time", "startup"),
            new("interval", "0s"),
            new("policy", DeploymentWatchdogScript.Policy),
            new("disabled", "no"),
        ];
        RejectDontRequireYes(scriptAttrs, findings);
        RejectDontRequireYes(deadlineAttrs, findings);
        RejectDontRequireYes(startupAttrs, findings);

        if (findings.Count > 0)
        {
            return Finish(findings, null);
        }

        return Finish(
            findings,
            new DeploymentWatchdogBundle
            {
                Token = token,
                DeviceId = devicePlan.DeviceId,
                ScriptName = scriptName,
                DeadlineSchedulerName = deadline,
                StartupSchedulerName = startup,
                ScriptSource = source,
                ScriptSourceHash = DeploymentWatchdogScript.HashSource(source),
                Ttl = devicePlan.RollbackTtl,
                ScriptAttributes = scriptAttrs,
                DeadlineAttributes = deadlineAttrs,
                StartupAttributes = startupAttrs,
            });
    }

    /// <summary>
    /// Spec §27 / AC#10: VRRP activation is forbidden until every member watchdog is armed.
    /// </summary>
    public static DeploymentWatchdogPlanResult EnsureAllDevicesArmed(
        IReadOnlyList<DeviceId> memberDeviceIds,
        IReadOnlySet<DeviceId> armedDeviceIds)
    {
        ArgumentNullException.ThrowIfNull(memberDeviceIds);
        ArgumentNullException.ThrowIfNull(armedDeviceIds);
        List<DeploymentWatchdogFinding> findings = [];
        foreach (DeviceId deviceId in memberDeviceIds.OrderBy(static d => d.Value))
        {
            if (!armedDeviceIds.Contains(deviceId))
            {
                findings.Add(Blocker(
                    DeploymentCodes.WatchdogNotArmed,
                    $"Device '{deviceId.Value:D}' watchdog is not armed; VRRP activation is blocked.",
                    deviceId.Value.ToString("D")));
            }
        }

        return Finish(findings, null);
    }

    private static void CheckOccupiedPrefix(DeploymentSystemNameFacts names, List<DeploymentWatchdogFinding> findings)
    {
        foreach (string name in names.ScriptNames.Concat(names.SchedulerNames))
        {
            if (DeploymentWatchdogNames.IsDeploymentWatchdogName(name))
            {
                findings.Add(Blocker(
                    DeploymentCodes.WatchdogScriptCollision,
                    $"Existing deployment watchdog name '{name}' blocks the operation.",
                    name));
            }
        }
    }

    private static void CheckCollision(
        DeploymentSystemNameFacts names,
        IReadOnlyList<string> planned,
        List<DeploymentWatchdogFinding> findings)
    {
        HashSet<string> existing = new(names.ScriptNames.Concat(names.SchedulerNames), StringComparer.Ordinal);
        foreach (string name in planned)
        {
            if (!existing.Contains(name))
            {
                continue;
            }

            string code = name.StartsWith("mfc-rb-s-", StringComparison.Ordinal)
                ? DeploymentCodes.WatchdogScriptCollision
                : DeploymentCodes.WatchdogSchedulerCollision;
            findings.Add(Blocker(code, $"Watchdog name '{name}' is already occupied.", name));
        }
    }

    private static void RejectDontRequireYes(
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        List<DeploymentWatchdogFinding> findings)
    {
        foreach (KeyValuePair<string, string> pair in attributes)
        {
            if (pair.Key == "dont-require-permissions"
                && !string.Equals(pair.Value, "no", StringComparison.Ordinal))
            {
                findings.Add(Blocker(
                    DeploymentCodes.WatchdogScriptInvalid,
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

    private static DeploymentWatchdogFinding Blocker(string code, string message, string? target)
        => new()
        {
            Code = code,
            Severity = DeploymentCodes.SeverityBlocker,
            Message = message,
            Target = target,
        };

    private static DeploymentWatchdogPlanResult Finish(
        List<DeploymentWatchdogFinding> findings,
        DeploymentWatchdogBundle? watchdog)
    {
        IReadOnlyList<DeploymentWatchdogFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Message, f.Target))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();
        return new DeploymentWatchdogPlanResult
        {
            Findings = ordered,
            Watchdog = watchdog,
        };
    }
}
