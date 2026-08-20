using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Application.Deployment;

/// <summary>One write-ahead journal entry for an anchor activation step (Spec §16 / AC#11).</summary>
public sealed class AnchorActivationJournalEntry
{
    public required AnchorKey Key { get; init; }

    public required DeploymentStepState State { get; init; }

    public required string? ObservedBefore { get; init; }

    public required string? ObservedAfter { get; init; }

    public required Hash256 ExpectedBeforeHash { get; init; }

    public required Hash256 DesiredAfterHash { get; init; }

    public string? Code { get; init; }
}

/// <summary>Aggregate permanent-anchor activation for one Device (Safe Deployment Spec §30 / M4-06).</summary>
public sealed class AnchorActivationResult
{
    public required bool Succeeded { get; init; }

    public required bool RecoveryRequired { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<AnchorActivationJournalEntry> Journal { get; init; }

    public required int SetCount { get; init; }

    public required int ReadCount { get; init; }
}

/// <summary>
/// Activates permanent anchors via typed jump-target set (M4-06).
/// Re-reads before every set, never blind-retries, checks watchdog margin after each anchor,
/// and records intent + verified result in the step journal.
/// </summary>
public static class ActivateAnchorsUseCase
{
    public static async Task<AnchorActivationResult> ExecuteAsync(
        DeviceDeploymentPlan devicePlan,
        IRouterOsDeploymentSession session,
        Func<TimeSpan> remainingWatchdogTtl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(remainingWatchdogTtl);

        Dictionary<string, string> oldBy = devicePlan.OldAnchorTargets
            .ToDictionary(static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal);
        Dictionary<string, string> newBy = devicePlan.NewAnchorTargets
            .ToDictionary(static t => t.Key.Marker, static t => t.JumpTarget, StringComparer.Ordinal);

        List<AnchorActivationJournalEntry> journal = [];
        int setCount = 0;
        int readCount = 0;

        foreach (AnchorKey key in devicePlan.AnchorActivationOrder)
        {
            string expectedOld = oldBy[key.Marker];
            string desiredNew = newBy[key.Marker];
            Hash256 beforeHash = TransitionStateValidator.HashState([new AnchorTarget(key, expectedOld)]);
            Hash256 afterHash = TransitionStateValidator.HashState([new AnchorTarget(key, desiredNew)]);

            (bool ok, string? observed, string? error, string? code, bool recovery, bool identityOk) =
                await ReadJumpTargetAsync(session, key, cancellationToken).ConfigureAwait(false);
            readCount++;
            if (!ok)
            {
                journal.Add(FailedEntry(key, observed, beforeHash, afterHash, code ?? DeploymentCodes.AnchorReadbackFailed));
                return Fail(code ?? DeploymentCodes.AnchorReadbackFailed, error, journal, setCount, readCount, recovery);
            }

            AnchorActivationDecision decision = AnchorActivationPlanner.Decide(
                observed,
                expectedOld,
                desiredNew,
                identityOk);
            if (decision.Action == AnchorActivationAction.RecoveryRequired)
            {
                journal.Add(FailedEntry(key, observed, beforeHash, afterHash, DeploymentCodes.RecoveryRequired));
                return Fail(DeploymentCodes.RecoveryRequired, "Unknown anchor target requires recovery.", journal, setCount, readCount, recovery: true);
            }

            if (decision.Action == AnchorActivationAction.PreconditionFailed)
            {
                journal.Add(FailedEntry(key, observed, beforeHash, afterHash, DeploymentCodes.AnchorPreconditionFailed));
                return Fail(
                    DeploymentCodes.AnchorPreconditionFailed,
                    "Anchor precondition read failed.",
                    journal,
                    setCount,
                    readCount,
                    recovery: false);
            }

            if (decision.Action == AnchorActivationAction.AlreadyApplied)
            {
                journal.Add(new AnchorActivationJournalEntry
                {
                    Key = key,
                    State = DeploymentStepState.Verified,
                    ObservedBefore = observed,
                    ObservedAfter = observed,
                    ExpectedBeforeHash = beforeHash,
                    DesiredAfterHash = afterHash,
                    Code = decision.Code,
                });
                if (!EnsureMargin(remainingWatchdogTtl, journal, key, beforeHash, afterHash, setCount, readCount, out AnchorActivationResult? marginFail))
                {
                    return marginFail!;
                }

                continue;
            }

            // Intent recorded before effect (AC#11).
            journal.Add(new AnchorActivationJournalEntry
            {
                Key = key,
                State = DeploymentStepState.IntentRecorded,
                ObservedBefore = observed,
                ObservedAfter = null,
                ExpectedBeforeHash = beforeHash,
                DesiredAfterHash = afterHash,
                Code = decision.Code,
            });

            DeploymentWriteExecutionResult written = await session.SetAnchorTargetAsync(
                new AnchorTargetWrite(key.Family, key.Chain, desiredNew),
                cancellationToken).ConfigureAwait(false);
            setCount++;
            readCount++; // SetAnchorTargetAsync always re-reads.

            if (!written.Succeeded)
            {
                // Unknown/failed set → classify by fresh read; never blind-retry (AC#7 / AC#8 / Spec §31).
                (bool reOk, string? reObserved, string? reError, string? reCode, bool reRecovery, bool reIdentity) =
                    await ReadJumpTargetAsync(session, key, cancellationToken).ConfigureAwait(false);
                readCount++;
                if (!reOk)
                {
                    MarkLast(journal, DeploymentStepState.Failed, reObserved, reCode ?? DeploymentCodes.AnchorSetFailed);
                    return Fail(
                        reCode ?? DeploymentCodes.AnchorSetFailed,
                        reError ?? written.Error,
                        journal,
                        setCount,
                        readCount,
                        reRecovery);
                }

                AnchorActivationDecision afterUnknown = AnchorActivationPlanner.ClassifyAfterUnknownSet(
                    reObserved,
                    expectedOld,
                    desiredNew);
                if (afterUnknown.Action == AnchorActivationAction.AlreadyApplied)
                {
                    MarkLast(journal, DeploymentStepState.Verified, reObserved, afterUnknown.Code);
                    if (!EnsureMargin(remainingWatchdogTtl, journal, key, beforeHash, afterHash, setCount, readCount, out AnchorActivationResult? m1))
                    {
                        return m1!;
                    }

                    continue;
                }

                if (AnchorActivationPlanner.AllowsControlledRetry(afterUnknown))
                {
                    DeploymentWriteExecutionResult retry = await session.SetAnchorTargetAsync(
                        new AnchorTargetWrite(key.Family, key.Chain, desiredNew),
                        cancellationToken).ConfigureAwait(false);
                    setCount++;
                    readCount++;
                    if (!retry.Succeeded
                        || !string.Equals(
                            retry.ReadBack.GetValueOrDefault("jump-target"),
                            desiredNew,
                            StringComparison.Ordinal))
                    {
                        (bool finalOk, string? finalTarget, _, string? finalCode, bool finalRecovery, _) =
                            await ReadJumpTargetAsync(session, key, cancellationToken).ConfigureAwait(false);
                        readCount++;
                        if (!finalOk)
                        {
                            MarkLast(journal, DeploymentStepState.Failed, finalTarget, finalCode ?? DeploymentCodes.AnchorSetFailed);
                            return Fail(
                                finalCode ?? DeploymentCodes.AnchorSetFailed,
                                retry.Error ?? "Controlled anchor set retry failed.",
                                journal,
                                setCount,
                                readCount,
                                finalRecovery);
                        }

                        AnchorActivationDecision final = AnchorActivationPlanner.ClassifyAfterUnknownSet(
                            finalTarget,
                            expectedOld,
                            desiredNew);
                        if (final.Action == AnchorActivationAction.AlreadyApplied)
                        {
                            MarkLast(journal, DeploymentStepState.Verified, finalTarget, final.Code);
                            if (!EnsureMargin(remainingWatchdogTtl, journal, key, beforeHash, afterHash, setCount, readCount, out AnchorActivationResult? mRetryOk))
                            {
                                return mRetryOk!;
                            }

                            continue;
                        }

                        MarkLast(
                            journal,
                            DeploymentStepState.Failed,
                            finalTarget,
                            final.Action == AnchorActivationAction.RecoveryRequired
                                ? DeploymentCodes.RecoveryRequired
                                : DeploymentCodes.AnchorSetFailed);
                        return Fail(
                            final.Action == AnchorActivationAction.RecoveryRequired
                                ? DeploymentCodes.RecoveryRequired
                                : DeploymentCodes.AnchorSetFailed,
                            retry.Error ?? "Controlled anchor set retry failed.",
                            journal,
                            setCount,
                            readCount,
                            final.Action == AnchorActivationAction.RecoveryRequired);
                    }

                    MarkLast(journal, DeploymentStepState.Verified, desiredNew, "ANCHOR_SET_VERIFIED");
                    if (!EnsureMargin(remainingWatchdogTtl, journal, key, beforeHash, afterHash, setCount, readCount, out AnchorActivationResult? m2))
                    {
                        return m2!;
                    }

                    continue;
                }

                MarkLast(
                    journal,
                    DeploymentStepState.Failed,
                    reObserved,
                    afterUnknown.Action == AnchorActivationAction.RecoveryRequired
                        ? DeploymentCodes.RecoveryRequired
                        : DeploymentCodes.AnchorSetFailed);
                return Fail(
                    afterUnknown.Action == AnchorActivationAction.RecoveryRequired
                        ? DeploymentCodes.RecoveryRequired
                        : DeploymentCodes.AnchorSetFailed,
                    written.Error ?? "Anchor set failed with non-retryable outcome.",
                    journal,
                    setCount,
                    readCount,
                    afterUnknown.Action == AnchorActivationAction.RecoveryRequired);
            }

            string? after = written.ReadBack.GetValueOrDefault("jump-target");
            if (!string.Equals(after, desiredNew, StringComparison.Ordinal))
            {
                MarkLast(journal, DeploymentStepState.Failed, after, DeploymentCodes.AnchorReadbackFailed);
                return Fail(
                    DeploymentCodes.AnchorReadbackFailed,
                    "Post-set jump-target read-back did not match desired new.",
                    journal,
                    setCount,
                    readCount,
                    recovery: AnchorActivationPlanner.Decide(after, expectedOld, desiredNew, true).Action
                              == AnchorActivationAction.RecoveryRequired);
            }

            MarkLast(journal, DeploymentStepState.Verified, after, "ANCHOR_SET_VERIFIED");
            if (!EnsureMargin(remainingWatchdogTtl, journal, key, beforeHash, afterHash, setCount, readCount, out AnchorActivationResult? marginFail2))
            {
                return marginFail2!;
            }
        }

        return new AnchorActivationResult
        {
            Succeeded = true,
            RecoveryRequired = false,
            Journal = journal,
            SetCount = setCount,
            ReadCount = readCount,
        };
    }

    private static async Task<(bool Ok, string? JumpTarget, string? Error, string? Code, bool Recovery, bool IdentityOk)> ReadJumpTargetAsync(
        IRouterOsDeploymentSession session,
        AnchorKey key,
        CancellationToken cancellationToken)
    {
        ActualManagedState state = await session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IReadOnlyDictionary<string, string>> rules = key.Family == IpAddressFamily.IPv4
            ? state.Ipv4FilterRules
            : state.Ipv6FilterRules;
        string chainName = key.Chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => string.Empty,
        };
        List<IReadOnlyDictionary<string, string>> matches = rules
            .Where(r =>
                string.Equals(r.GetValueOrDefault("comment"), key.Marker, StringComparison.Ordinal)
                && string.Equals(r.GetValueOrDefault("chain"), chainName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.GetValueOrDefault("action"), "jump", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0)
        {
            return (false, null, "Permanent anchor not found.", DeploymentCodes.AnchorPreconditionFailed, false, false);
        }

        if (matches.Count > 1)
        {
            return (false, null, "Duplicate permanent anchor marker.", DeploymentCodes.AnchorInvalid, true, false);
        }

        IReadOnlyDictionary<string, string> row = matches[0];
        string? jump = row.GetValueOrDefault("jump-target");
        bool identityOk = !string.IsNullOrWhiteSpace(row.GetValueOrDefault(".id"))
                          && string.Equals(row.GetValueOrDefault("action"), "jump", StringComparison.OrdinalIgnoreCase);
        return (true, jump, null, null, false, identityOk);
    }

    private static bool EnsureMargin(
        Func<TimeSpan> remainingWatchdogTtl,
        List<AnchorActivationJournalEntry> journal,
        AnchorKey key,
        Hash256 beforeHash,
        Hash256 afterHash,
        int setCount,
        int readCount,
        out AnchorActivationResult? failure)
    {
        if (AnchorActivationPlanner.HasWatchdogMargin(remainingWatchdogTtl()))
        {
            failure = null;
            return true;
        }

        journal.Add(FailedEntry(key, null, beforeHash, afterHash, DeploymentCodes.WatchdogDeadlineTooClose));
        failure = Fail(
            DeploymentCodes.WatchdogDeadlineTooClose,
            "Watchdog remaining TTL fell below the 30s commit margin.",
            journal,
            setCount,
            readCount,
            recovery: false);
        return false;
    }

    private static void MarkLast(
        List<AnchorActivationJournalEntry> journal,
        DeploymentStepState state,
        string? observedAfter,
        string? code)
    {
        AnchorActivationJournalEntry last = journal[^1];
        journal[^1] = new AnchorActivationJournalEntry
        {
            Key = last.Key,
            State = state,
            ObservedBefore = last.ObservedBefore,
            ObservedAfter = observedAfter,
            ExpectedBeforeHash = last.ExpectedBeforeHash,
            DesiredAfterHash = last.DesiredAfterHash,
            Code = code,
        };
    }

    private static AnchorActivationJournalEntry FailedEntry(
        AnchorKey key,
        string? observed,
        Hash256 beforeHash,
        Hash256 afterHash,
        string code)
        => new()
        {
            Key = key,
            State = DeploymentStepState.Failed,
            ObservedBefore = observed,
            ObservedAfter = observed,
            ExpectedBeforeHash = beforeHash,
            DesiredAfterHash = afterHash,
            Code = code,
        };

    private static AnchorActivationResult Fail(
        string code,
        string? message,
        IReadOnlyList<AnchorActivationJournalEntry> journal,
        int setCount,
        int readCount,
        bool recovery)
        => new()
        {
            Succeeded = false,
            RecoveryRequired = recovery,
            Code = code,
            Message = message,
            Journal = journal,
            SetCount = setCount,
            ReadCount = readCount,
        };
}
