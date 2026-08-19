using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Closed bootstrap writer (M5-05). Paths come from <see cref="OnboardingWritePath"/> only.
/// There is no free-form command method and no <c>/move</c>.
/// </summary>
public sealed class OnboardingBootstrapWriter : IOnboardingBootstrapWritePort
{
    public const string AnalyzerVersion = "mfc.routeros.onboarding_writer.v1";

    private readonly IOnboardingWriteChannel _channel;

    public OnboardingBootstrapWriter(IOnboardingWriteChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
    }

    public async Task<OnboardingBootstrapWriteExecutionResult> ApplyAsync(
        OnboardingBootstrapWrite write,
        IReadOnlyList<ActualFilterRule> liveSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(liveSnapshot);

        try
        {
            return write.Kind switch
            {
                OnboardingBootstrapWriteKind.AddBootstrapReturn
                    => await AddBootstrapReturnAsync(write, cancellationToken).ConfigureAwait(false),
                OnboardingBootstrapWriteKind.AddDisabledAnchor
                    => await AddDisabledAnchorAsync(write, liveSnapshot, cancellationToken).ConfigureAwait(false),
                OnboardingBootstrapWriteKind.SetAnchorDisabled
                    => await SetAnchorDisabledAsync(write, cancellationToken).ConfigureAwait(false),
                OnboardingBootstrapWriteKind.RemoveDisabledAnchor
                    => await RemoveExactAsync(write, disabledAnchor: true, cancellationToken).ConfigureAwait(false),
                OnboardingBootstrapWriteKind.RemoveBootstrapReturn
                    => await RemoveExactAsync(write, disabledAnchor: false, cancellationToken).ConfigureAwait(false),
                _ => throw new DomainInvariantException($"Unsupported onboarding write kind '{write.Kind}'."),
            };
        }
        catch (InvalidOperationException ex)
        {
            return Fail(write, OnboardingWritePaths.ForAdd(write.Family), ex.Message);
        }
    }

    private async Task<OnboardingBootstrapWriteExecutionResult> AddBootstrapReturnAsync(
        OnboardingBootstrapWrite write,
        CancellationToken cancellationToken)
    {
        OnboardingBootstrapWritePlanner.AssertSingleUnconditionalReturn(write);
        OnboardingWritePath path = OnboardingWritePaths.ForAdd(write.Family);
        await _channel.SendAsync(path, write.Attributes, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> readBack = await RequireReadBackAsync(
            write.Family,
            comment: BootstrapArtifact.ReturnComment,
            chain: write.RootChainName,
            cancellationToken).ConfigureAwait(false);
        EnsureReadBack(readBack, write.Attributes);
        return Ok(path, write.Attributes, readBack);
    }

    private async Task<OnboardingBootstrapWriteExecutionResult> AddDisabledAnchorAsync(
        OnboardingBootstrapWrite write,
        IReadOnlyList<ActualFilterRule> liveSnapshot,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(write.Attributes.Single(static a => a.Key == "disabled").Value, "yes", StringComparison.Ordinal))
        {
            throw new DomainInvariantException("Permanent anchor must be created disabled.");
        }

        if (!string.Equals(
                write.Attributes.Single(static a => a.Key == "jump-target").Value,
                write.RootChainName,
                StringComparison.Ordinal))
        {
            throw new DomainInvariantException("Anchor jump-target must be the bootstrap root.");
        }

        List<KeyValuePair<string, string>> sent = [.. write.Attributes];
        if (write.PlacementMode == AnchorPlacementMode.BeforeStaticRule)
        {
            string itemId = ResolvePlaceBeforeId(write, liveSnapshot, await _channel.PrintAsync(write.Family, cancellationToken).ConfigureAwait(false));
            sent.Add(new("place-before", itemId));
        }

        if (sent.Any(static a => a.Key.Contains("move", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainInvariantException("Onboarding writer must not use move.");
        }

        OnboardingWritePath path = OnboardingWritePaths.ForAdd(write.Family);
        await _channel.SendAsync(path, sent, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> readBack = await RequireReadBackAsync(
            write.Family,
            comment: write.AnchorMarker,
            chain: write.BuiltinChainName,
            cancellationToken).ConfigureAwait(false);
        EnsureReadBack(readBack, write.Attributes);
        return Ok(path, sent, readBack);
    }

    private async Task<OnboardingBootstrapWriteExecutionResult> SetAnchorDisabledAsync(
        OnboardingBootstrapWrite write,
        CancellationToken cancellationToken)
    {
        if (write.Attributes.Count != 1 || write.Attributes[0].Key != "disabled")
        {
            throw new DomainInvariantException("set allows only the anchor disabled flag.");
        }

        IReadOnlyDictionary<string, string> existing = await RequireReadBackAsync(
            write.Family,
            comment: write.AnchorMarker,
            chain: write.BuiltinChainName,
            cancellationToken).ConfigureAwait(false);
        if (!existing.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("Cannot set disabled without a live .id from read-back.");
        }

        KeyValuePair<string, string>[] sent =
        [
            new(".id", itemId),
            write.Attributes[0],
        ];
        OnboardingWritePath path = OnboardingWritePaths.ForSet(write.Family);
        await _channel.SendAsync(path, sent, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> readBack = await RequireReadBackAsync(
            write.Family,
            comment: write.AnchorMarker,
            chain: write.BuiltinChainName,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(readBack.GetValueOrDefault("disabled"), write.Attributes[0].Value, StringComparison.OrdinalIgnoreCase)
            && !DisabledEquals(readBack.GetValueOrDefault("disabled"), write.DisabledValue == true))
        {
            throw new InvalidOperationException("Read-back disabled flag does not match the set.");
        }

        return Ok(path, sent, readBack);
    }

    private async Task<OnboardingBootstrapWriteExecutionResult> RemoveExactAsync(
        OnboardingBootstrapWrite write,
        bool disabledAnchor,
        CancellationToken cancellationToken)
    {
        string comment = disabledAnchor ? write.AnchorMarker : BootstrapArtifact.ReturnComment;
        string chain = disabledAnchor ? write.BuiltinChainName : write.RootChainName;
        IReadOnlyDictionary<string, string> existing = await RequireReadBackAsync(
            write.Family,
            comment,
            chain,
            cancellationToken).ConfigureAwait(false);

        if (disabledAnchor)
        {
            if (!DisabledEquals(existing.GetValueOrDefault("disabled"), true))
            {
                throw new InvalidOperationException("remove is allowed only for a disabled onboarding anchor.");
            }

            if (!string.Equals(existing.GetValueOrDefault("comment"), write.AnchorMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("remove target is not the exact onboarding anchor.");
            }
        }
        else if (!string.Equals(existing.GetValueOrDefault("comment"), BootstrapArtifact.ReturnComment, StringComparison.Ordinal)
                 || !string.Equals(existing.GetValueOrDefault("chain"), write.RootChainName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("remove target is not the exact bootstrap return.");
        }

        if (!existing.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("Cannot remove without a live .id from read-back.");
        }

        KeyValuePair<string, string>[] sent = [new(".id", itemId)];
        OnboardingWritePath path = OnboardingWritePaths.ForRemove(write.Family);
        await _channel.SendAsync(path, sent, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IReadOnlyDictionary<string, string>> after = await _channel.PrintAsync(write.Family, cancellationToken)
            .ConfigureAwait(false);
        if (after.Any(r => string.Equals(r.GetValueOrDefault("comment"), comment, StringComparison.Ordinal)
                           && string.Equals(r.GetValueOrDefault("chain"), chain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Read-back still contains the removed onboarding resource.");
        }

        return Ok(path, sent, existing);
    }

    private async Task<IReadOnlyDictionary<string, string>> RequireReadBackAsync(
        IpAddressFamily family,
        string comment,
        string chain,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = await _channel.PrintAsync(family, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, string>? match = rows.FirstOrDefault(r =>
            string.Equals(r.GetValueOrDefault("comment"), comment, StringComparison.Ordinal)
            && string.Equals(r.GetValueOrDefault("chain"), chain, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new InvalidOperationException($"Read-back did not find comment '{comment}' on chain '{chain}'.");
        }

        return match;
    }

    private static string ResolvePlaceBeforeId(
        OnboardingBootstrapWrite write,
        IReadOnlyList<ActualFilterRule> liveSnapshot,
        IReadOnlyList<IReadOnlyDictionary<string, string>> printed)
    {
        ArgumentNullException.ThrowIfNull(write.PlaceBeforeFingerprint);
        List<ActualFilterRule> chainRules = liveSnapshot
            .Where(r => r.Family == write.Family
                        && string.Equals(r.Chain, write.BuiltinChainName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static r => r.Ordinal)
            .ToList();
        List<ActualFilterRule> matches = chainRules
            .Where(r => FilterRuleFingerprint.Compute(r).Equals(write.PlaceBeforeFingerprint))
            .ToList();
        uint rank = write.PlaceBeforeRank ?? 0;
        if (rank >= (uint)matches.Count)
        {
            throw new InvalidOperationException("place-before reference fingerprint/rank is missing in the live snapshot.");
        }

        ActualFilterRule target = matches[(int)rank];
        List<IReadOnlyDictionary<string, string>> printedChain = printed
            .Where(r => string.Equals(r.GetValueOrDefault("chain"), target.Chain, StringComparison.OrdinalIgnoreCase))
            .ToList();
        int index = chainRules.FindIndex(r => r.Ordinal == target.Ordinal);
        if (index < 0 || index >= printedChain.Count)
        {
            throw new InvalidOperationException("place-before live .id could not be resolved from print order.");
        }

        IReadOnlyDictionary<string, string> row = printedChain[index];
        if (!row.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("place-before live .id could not be resolved from print.");
        }

        return itemId;
    }

    private static void EnsureReadBack(
        IReadOnlyDictionary<string, string> readBack,
        IReadOnlyList<KeyValuePair<string, string>> expected)
    {
        foreach (KeyValuePair<string, string> pair in expected)
        {
            if (pair.Key is "place-before" or ".id")
            {
                continue;
            }

            string? actual = readBack.GetValueOrDefault(pair.Key);
            if (pair.Key == "disabled")
            {
                if (!DisabledEquals(actual, pair.Value is "yes" or "true"))
                {
                    throw new InvalidOperationException($"Read-back '{pair.Key}' mismatch.");
                }

                continue;
            }

            if (!string.Equals(actual, pair.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Read-back '{pair.Key}' mismatch.");
            }
        }
    }

    private static bool DisabledEquals(string? raw, bool expectedYes)
    {
        bool isYes = raw is "yes" or "true" or "1";
        return expectedYes ? isYes : !isYes;
    }

    private static OnboardingBootstrapWriteExecutionResult Ok(
        OnboardingWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> sent,
        IReadOnlyDictionary<string, string> readBack)
        => new()
        {
            Succeeded = true,
            Path = OnboardingWritePaths.Fixed(path),
            SentAttributes = sent,
            ReadBack = readBack,
        };

    private static OnboardingBootstrapWriteExecutionResult Fail(
        OnboardingBootstrapWrite write,
        OnboardingWritePath path,
        string error)
        => new()
        {
            Succeeded = false,
            Path = OnboardingWritePaths.Fixed(path),
            SentAttributes = write.Attributes,
            ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
            Error = error,
        };
}
