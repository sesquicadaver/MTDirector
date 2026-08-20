using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>Caller-supplied safety verdict for one intermediate anchor combination (Spec §29).</summary>
public sealed class TransitionStateEvidence
{
    public TransitionStateEvidence(int stateIndex, bool isSafe, string? detailCode = null)
    {
        if (stateIndex < 0)
        {
            throw new DomainInvariantException("transition state index must be >= 0.");
        }

        StateIndex = stateIndex;
        IsSafe = isSafe;
        DetailCode = string.IsNullOrWhiteSpace(detailCode) ? null : detailCode.Trim();
    }

    public int StateIndex { get; }

    public bool IsSafe { get; }

    public string? DetailCode { get; }
}

/// <summary>One intermediate old/new combination with a content hash (Spec §29).</summary>
public sealed class TransitionStateSnapshot
{
    public required int Index { get; init; }

    public required IReadOnlyList<AnchorTarget> Targets { get; init; }

    public required Hash256 ContentHash { get; init; }

    public required bool IsSafe { get; init; }
}

/// <summary>One transition-state validation finding.</summary>
public sealed class TransitionStateFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Target { get; init; }
}

/// <summary>Outcome of <see cref="TransitionStateValidator"/>.</summary>
public sealed class TransitionStateValidationResult
{
    public required IReadOnlyList<TransitionStateFinding> Findings { get; init; }

    public required IReadOnlyList<TransitionStateSnapshot> States { get; init; }

    public required IReadOnlyList<Hash256> TransitionStateHashes { get; init; }

    public bool Passed => Findings.Count == 0
        || Findings.All(static f => f.Severity != DeploymentCodes.SeverityBlocker);

    public bool HasBlockers => Findings.Any(static f => f.Severity == DeploymentCodes.SeverityBlocker);
}

/// <summary>
/// Builds and validates every intermediate old→new anchor combination (Safe Deployment Spec §29 / M4-06).
/// Does not execute RouterOS writes; unsafe evidence blocks plan sealing.
/// </summary>
public static class TransitionStateValidator
{
    public const string AnalyzerVersion = "mfc.deployment.transition.v1";

    /// <summary>
    /// Enumerate states 0..N (all old … all new), require evidence for each, hash targets, block on unsafe.
    /// </summary>
    public static TransitionStateValidationResult Validate(
        IReadOnlyList<AnchorKey> activationOrder,
        IReadOnlyList<AnchorTarget> oldTargets,
        IReadOnlyList<AnchorTarget> newTargets,
        IReadOnlyList<TransitionStateEvidence> evidence,
        IReadOnlyDictionary<AnchorKey, AnchorActivationCriticality>? criticality = null)
    {
        ArgumentNullException.ThrowIfNull(activationOrder);
        ArgumentNullException.ThrowIfNull(oldTargets);
        ArgumentNullException.ThrowIfNull(newTargets);
        ArgumentNullException.ThrowIfNull(evidence);

        List<TransitionStateFinding> findings = [];
        if (activationOrder.Count == 0)
        {
            findings.Add(Blocker(DeploymentCodes.ActivationOrderInvalid, "Activation order is empty.", "order"));
            return Finish(findings, [], []);
        }

        if (!DeploymentAnchorOrder.IsManagementCriticalLast(activationOrder, criticality))
        {
            findings.Add(Blocker(
                DeploymentCodes.ActivationOrderInvalid,
                "Management-critical anchors must be activated last.",
                "order"));
        }

        Dictionary<string, string> oldByMarker = ToMap(oldTargets, "old", findings);
        Dictionary<string, string> newByMarker = ToMap(newTargets, "new", findings);
        foreach (AnchorKey key in activationOrder)
        {
            if (!oldByMarker.ContainsKey(key.Marker) || !newByMarker.ContainsKey(key.Marker))
            {
                findings.Add(Blocker(
                    DeploymentCodes.AnchorInvalid,
                    "Activation key missing from old/new target sets.",
                    key.Marker));
            }
        }

        int expectedStates = activationOrder.Count + 1;
        Dictionary<int, TransitionStateEvidence> byIndex = [];
        foreach (TransitionStateEvidence item in evidence)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!byIndex.TryAdd(item.StateIndex, item))
            {
                findings.Add(Blocker(
                    DeploymentCodes.TransitionStateUnsafe,
                    "Duplicate transition-state evidence index.",
                    item.StateIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        for (int i = 0; i < expectedStates; i++)
        {
            if (!byIndex.ContainsKey(i))
            {
                findings.Add(Blocker(
                    DeploymentCodes.TransitionStateUnsafe,
                    "Missing evidence for an intermediate old/new combination.",
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        if (byIndex.Keys.Any(k => k < 0 || k >= expectedStates))
        {
            findings.Add(Blocker(
                DeploymentCodes.TransitionStateUnsafe,
                "Transition-state evidence index is outside 0..N.",
                "evidence"));
        }

        if (findings.Count > 0)
        {
            return Finish(findings, [], []);
        }

        List<TransitionStateSnapshot> states = [];
        List<Hash256> hashes = [];
        for (int i = 0; i < expectedStates; i++)
        {
            List<AnchorTarget> targets = [];
            for (int a = 0; a < activationOrder.Count; a++)
            {
                AnchorKey key = activationOrder[a];
                string jump = a < i ? newByMarker[key.Marker] : oldByMarker[key.Marker];
                targets.Add(new AnchorTarget(key, jump));
            }

            TransitionStateEvidence ev = byIndex[i];
            Hash256 hash = HashState(targets);
            states.Add(new TransitionStateSnapshot
            {
                Index = i,
                Targets = targets,
                ContentHash = hash,
                IsSafe = ev.IsSafe,
            });
            hashes.Add(hash);
            if (!ev.IsSafe)
            {
                findings.Add(Blocker(
                    ev.DetailCode ?? DeploymentCodes.TransitionStateUnsafe,
                    "Intermediate old/new combination is not proven safe.",
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        return Finish(findings, states, hashes);
    }

    /// <summary>Canonical content hash of one intermediate target set (markers sorted).</summary>
    public static Hash256 HashState(IReadOnlyList<AnchorTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        AppendUInt32Be(hasher, (uint)targets.Count);
        foreach (AnchorTarget target in targets.OrderBy(static t => t.Key.Marker, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, target.Key.Marker);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, target.JumpTarget);
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>All-safe evidence covering states 0..N for tests and sealed plans with proven analysis.</summary>
    public static IReadOnlyList<TransitionStateEvidence> AllSafeEvidence(int activationCount)
    {
        if (activationCount < 1)
        {
            throw new DomainInvariantException("activation count must be >= 1.");
        }

        TransitionStateEvidence[] items = new TransitionStateEvidence[activationCount + 1];
        for (int i = 0; i <= activationCount; i++)
        {
            items[i] = new TransitionStateEvidence(i, isSafe: true);
        }

        return items;
    }

    private static Dictionary<string, string> ToMap(
        IReadOnlyList<AnchorTarget> targets,
        string label,
        List<TransitionStateFinding> findings)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (AnchorTarget target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (!map.TryAdd(target.Key.Marker, target.JumpTarget))
            {
                findings.Add(Blocker(
                    DeploymentCodes.AnchorInvalid,
                    $"{label} anchor targets contain a duplicate key.",
                    target.Key.Marker));
            }
        }

        return map;
    }

    private static TransitionStateFinding Blocker(string code, string message, string? target)
        => new()
        {
            Code = code,
            Severity = DeploymentCodes.SeverityBlocker,
            Message = message,
            Target = target,
        };

    private static TransitionStateValidationResult Finish(
        List<TransitionStateFinding> findings,
        List<TransitionStateSnapshot> states,
        List<Hash256> hashes)
        => new()
        {
            Findings = findings,
            States = states,
            TransitionStateHashes = hashes,
        };

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendUInt32Be(IncrementalHash hasher, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        hasher.AppendData(buf);
    }
}
