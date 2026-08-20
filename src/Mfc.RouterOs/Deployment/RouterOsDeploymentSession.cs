using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Restricted managed-resource writer and managed-state reader (Safe Deployment Spec §6–§8 / M4-02).
/// Lives in <c>Mfc.RouterOs.Deployment</c> — not <c>Mfc.RouterOs.Write</c>.
/// </summary>
public sealed class RouterOsDeploymentSession : IRouterOsDeploymentSession
{
    public const string AnalyzerVersion = "mfc.routeros.deployment_writer.v1";

    private static readonly HashSet<string> FilterSetAllowedKeys =
        new(StringComparer.Ordinal) { ".id", "jump-target" };

    private static readonly HashSet<string> SchedulerSetAllowedKeys =
        new(StringComparer.Ordinal) { ".id", "disabled" };

    private readonly IDeploymentWriteChannel _channel;
    private bool _disposed;

    public RouterOsDeploymentSession(IDeploymentWriteChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
    }

    public async Task<ActualManagedState> ReadManagedStateAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        return new ActualManagedState
        {
            Ipv4FilterRules = await _channel.PrintAsync(DeploymentReadSurface.Ipv4Filter, cancellationToken)
                .ConfigureAwait(false),
            Ipv6FilterRules = await _channel.PrintAsync(DeploymentReadSurface.Ipv6Filter, cancellationToken)
                .ConfigureAwait(false),
            Ipv4AddressLists = await _channel.PrintAsync(DeploymentReadSurface.Ipv4AddressList, cancellationToken)
                .ConfigureAwait(false),
            Ipv6AddressLists = await _channel.PrintAsync(DeploymentReadSurface.Ipv6AddressList, cancellationToken)
                .ConfigureAwait(false),
            Scripts = await _channel.PrintAsync(DeploymentReadSurface.Script, cancellationToken)
                .ConfigureAwait(false),
            Schedulers = await _channel.PrintAsync(DeploymentReadSurface.Scheduler, cancellationToken)
                .ConfigureAwait(false),
        };
    }

    public async Task<DeploymentWriteExecutionResult> AddAddressListEntryAsync(
        AddressListEntryWrite write,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(write);
        List<KeyValuePair<string, string>> attrs =
        [
            new("list", write.ListName),
            new("address", write.Address),
        ];
        if (write.Comment is not null)
        {
            attrs.Add(new("comment", write.Comment));
        }

        DeploymentWritePath path = DeploymentWritePaths.ForAddressListAdd(write.Family);
        try
        {
            await _channel.SendAsync(path, attrs, cancellationToken).ConfigureAwait(false);
            DeploymentReadSurface surface = write.Family == IpAddressFamily.IPv4
                ? DeploymentReadSurface.Ipv4AddressList
                : DeploymentReadSurface.Ipv6AddressList;
            IReadOnlyDictionary<string, string> readBack = await RequireUniqueAsync(
                surface,
                static r => true,
                r => string.Equals(r.GetValueOrDefault("list"), write.ListName, StringComparison.Ordinal)
                     && string.Equals(r.GetValueOrDefault("address"), write.Address, StringComparison.Ordinal),
                "address-list entry",
                cancellationToken).ConfigureAwait(false);
            EnsureReadBack(readBack, attrs);
            return Ok(path, attrs, readBack, TrySessionId(readBack));
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, attrs, ex.Message);
        }
    }

    public async Task<DeploymentWriteExecutionResult> AddFilterRuleAsync(
        FilterRuleWrite write,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(write);
        List<KeyValuePair<string, string>> attrs =
        [
            new("chain", write.Chain),
            new("action", write.Action),
        ];
        if (write.JumpTarget is not null)
        {
            attrs.Add(new("jump-target", write.JumpTarget));
        }

        if (write.Comment is not null)
        {
            attrs.Add(new("comment", write.Comment));
        }

        if (write.Disabled is bool disabled)
        {
            attrs.Add(new("disabled", disabled ? "yes" : "no"));
        }

        foreach ((string key, string value) in write.AdditionalMatchers.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            attrs.Add(new(key, value));
        }

        if (attrs.Any(static a => a.Key.Contains("move", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainInvariantException("Deployment writer must not use move.");
        }

        DeploymentWritePath path = DeploymentWritePaths.ForFilterAdd(write.Family);
        try
        {
            await _channel.SendAsync(path, attrs, cancellationToken).ConfigureAwait(false);
            DeploymentReadSurface surface = write.Family == IpAddressFamily.IPv4
                ? DeploymentReadSurface.Ipv4Filter
                : DeploymentReadSurface.Ipv6Filter;
            IReadOnlyDictionary<string, string> readBack = await RequireUniqueAsync(
                surface,
                static r => true,
                r => MatchesFilterIdentity(r, write),
                "filter rule",
                cancellationToken).ConfigureAwait(false);
            EnsureReadBack(readBack, attrs.Where(static a => a.Key != "place-before").ToArray());
            return Ok(path, attrs, readBack, TrySessionId(readBack));
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, attrs, ex.Message);
        }
    }

    public async Task<DeploymentWriteExecutionResult> SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(write);
        DeploymentWritePath path = DeploymentWritePaths.ForFilterSet(write.Family);
        List<KeyValuePair<string, string>> sent = [];
        try
        {
            DeploymentReadSurface surface = write.Family == IpAddressFamily.IPv4
                ? DeploymentReadSurface.Ipv4Filter
                : DeploymentReadSurface.Ipv6Filter;
            string chainName = BuiltinChainName(write.Chain);
            IReadOnlyDictionary<string, string> existing = await RequireUniqueAsync(
                surface,
                static r => true,
                r => string.Equals(r.GetValueOrDefault("comment"), write.OwnershipMarker, StringComparison.Ordinal)
                     && string.Equals(r.GetValueOrDefault("chain"), chainName, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(r.GetValueOrDefault("action"), "jump", StringComparison.OrdinalIgnoreCase),
                "permanent anchor",
                cancellationToken).ConfigureAwait(false);

            if (!existing.TryGetValue(".id", out string? itemId) || string.IsNullOrWhiteSpace(itemId))
            {
                throw new InvalidOperationException("Cannot set jump-target without a live .id from read-back.");
            }

            // Spec §7.1: filter/set may change only .id + jump-target for a valid ownership marker.
            sent =
            [
                new(".id", itemId),
                new("jump-target", write.JumpTarget),
            ];
            EnsureFilterSetAllowlist(sent);
            await _channel.SendAsync(path, sent, cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, string> readBack = await RequireUniqueAsync(
                surface,
                static r => true,
                r => string.Equals(r.GetValueOrDefault("comment"), write.OwnershipMarker, StringComparison.Ordinal)
                     && string.Equals(r.GetValueOrDefault("chain"), chainName, StringComparison.OrdinalIgnoreCase),
                "permanent anchor",
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(readBack.GetValueOrDefault("jump-target"), write.JumpTarget, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Read-back jump-target does not match the set.");
            }

            return Ok(path, sent, readBack, RouterOsItemId.Create(itemId));
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, sent, ex.Message);
        }
    }

    public async Task<DeploymentWriteExecutionResult> AddRollbackScriptAsync(
        RollbackScriptWrite write,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(write);
        List<KeyValuePair<string, string>> attrs =
        [
            new("name", write.Name),
            new("source", write.Source),
            new("dont-require-permissions", "no"),
        ];
        DeploymentWritePath path = DeploymentWritePath.SystemScriptAdd;
        try
        {
            await _channel.SendAsync(path, attrs, cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, string> readBack = await RequireUniqueAsync(
                DeploymentReadSurface.Script,
                static r => true,
                r => string.Equals(r.GetValueOrDefault("name"), write.Name, StringComparison.Ordinal),
                "rollback script",
                cancellationToken).ConfigureAwait(false);
            string source = readBack.GetValueOrDefault("source") ?? string.Empty;
            Hash256 observed = Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            if (!observed.Equals(write.SourceHash))
            {
                throw new InvalidOperationException("Rollback script source hash mismatch.");
            }

            return Ok(path, attrs, readBack, TrySessionId(readBack));
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, attrs, ex.Message);
        }
    }

    public async Task<DeploymentWriteExecutionResult> AddRollbackSchedulerAsync(
        RollbackSchedulerWrite write,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(write);
        List<KeyValuePair<string, string>> attrs =
        [
            new("name", write.Name),
            new("on-event", write.OnEvent),
        ];
        if (write.StartTime is not null)
        {
            attrs.Add(new("start-time", write.StartTime));
        }

        if (write.StartDate is not null)
        {
            attrs.Add(new("start-date", write.StartDate));
        }

        if (write.Interval is not null)
        {
            attrs.Add(new("interval", write.Interval));
        }

        DeploymentWritePath path = DeploymentWritePath.SystemSchedulerAdd;
        try
        {
            await _channel.SendAsync(path, attrs, cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, string> readBack = await RequireUniqueAsync(
                DeploymentReadSurface.Scheduler,
                static r => true,
                r => string.Equals(r.GetValueOrDefault("name"), write.Name, StringComparison.Ordinal),
                "rollback scheduler",
                cancellationToken).ConfigureAwait(false);
            EnsureReadBack(readBack, attrs);
            return Ok(path, attrs, readBack, TrySessionId(readBack));
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, attrs, ex.Message);
        }
    }

    public async Task<DeploymentWriteExecutionResult> DisableRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        List<KeyValuePair<string, string>> sent =
        [
            new(".id", schedulerId.Value),
            new("disabled", "yes"),
        ];
        EnsureSchedulerSetAllowlist(sent);
        DeploymentWritePath path = DeploymentWritePath.SystemSchedulerSet;
        try
        {
            await _channel.SendAsync(path, sent, cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, string> readBack = await RequireUniqueAsync(
                DeploymentReadSurface.Scheduler,
                static r => true,
                r => string.Equals(r.GetValueOrDefault(".id"), schedulerId.Value, StringComparison.Ordinal),
                "rollback scheduler",
                cancellationToken).ConfigureAwait(false);
            if (!Yes(readBack.GetValueOrDefault("disabled")))
            {
                throw new InvalidOperationException("Read-back scheduler is not disabled.");
            }

            return Ok(path, sent, readBack, schedulerId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, sent, ex.Message);
        }
    }

    public async Task<DeploymentWriteExecutionResult> RemoveRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken = default)
        => await RemoveByIdAsync(
            DeploymentWritePath.SystemSchedulerRemove,
            DeploymentReadSurface.Scheduler,
            schedulerId,
            cancellationToken).ConfigureAwait(false);

    public async Task<DeploymentWriteExecutionResult> RemoveRollbackScriptAsync(
        RouterOsItemId scriptId,
        CancellationToken cancellationToken = default)
        => await RemoveByIdAsync(
            DeploymentWritePath.SystemScriptRemove,
            DeploymentReadSurface.Script,
            scriptId,
            cancellationToken).ConfigureAwait(false);

    public async Task<RouterPingResult> PingAsync(
        RouterPingRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(request);
        List<KeyValuePair<string, string>> attrs =
        [
            new("address", request.Destination.ToString()),
            new("count", request.Count.ToString(CultureInfo.InvariantCulture)),
            new("interval", "100ms"),
            new("timeout", $"{request.TimeoutMilliseconds}ms"),
        ];
        if (request.SourceAddress is not null)
        {
            attrs.Add(new("src-address", request.SourceAddress.ToString()));
        }

        if (request.RoutingTable is not null)
        {
            attrs.Add(new("routing-table", request.RoutingTable));
        }

        if (request.Interface is not null)
        {
            attrs.Add(new("interface", request.Interface));
        }

        ChannelPingResult raw = await _channel.PingAsync(attrs, cancellationToken).ConfigureAwait(false);
        RouterPingOutcome outcome = raw.Received == request.Count
            ? RouterPingOutcome.Pass
            : raw.Received == 0
                ? RouterPingOutcome.Fail
                : RouterPingOutcome.Inconclusive;
        return new RouterPingResult
        {
            Outcome = outcome,
            Sent = raw.Sent,
            Received = raw.Received,
            Detail = raw.Detail,
        };
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private async Task<DeploymentWriteExecutionResult> RemoveByIdAsync(
        DeploymentWritePath path,
        DeploymentReadSurface surface,
        RouterOsItemId itemId,
        CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        List<KeyValuePair<string, string>> sent = [new(".id", itemId.Value)];
        try
        {
            IReadOnlyDictionary<string, string> existing = await RequireUniqueAsync(
                surface,
                static r => true,
                r => string.Equals(r.GetValueOrDefault(".id"), itemId.Value, StringComparison.Ordinal),
                "named resource",
                cancellationToken).ConfigureAwait(false);
            await _channel.SendAsync(path, sent, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<IReadOnlyDictionary<string, string>> after = await _channel.PrintAsync(surface, cancellationToken)
                .ConfigureAwait(false);
            if (after.Any(r => string.Equals(r.GetValueOrDefault(".id"), itemId.Value, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Read-back still contains the removed resource.");
            }

            return Ok(path, sent, existing, itemId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainInvariantException)
        {
            return Fail(path, sent, ex.Message);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> RequireUniqueAsync(
        DeploymentReadSurface surface,
        Func<IReadOnlyDictionary<string, string>, bool> prefilter,
        Func<IReadOnlyDictionary<string, string>, bool> match,
        string label,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = await _channel.PrintAsync(surface, cancellationToken)
            .ConfigureAwait(false);
        List<IReadOnlyDictionary<string, string>> hits = rows.Where(prefilter).Where(match).ToList();
        if (hits.Count != 1)
        {
            throw new InvalidOperationException($"Expected exactly one {label}, found {hits.Count}.");
        }

        return hits[0];
    }

    private static void EnsureFilterSetAllowlist(List<KeyValuePair<string, string>> sent)
    {
        if (sent.Any(a => !FilterSetAllowedKeys.Contains(a.Key)))
        {
            throw new DomainInvariantException("filter/set allows only .id and jump-target.");
        }

        if (sent.Count != 2
            || sent.All(static a => a.Key != ".id")
            || sent.All(static a => a.Key != "jump-target"))
        {
            throw new DomainInvariantException("filter/set requires exactly .id and jump-target.");
        }
    }

    private static void EnsureSchedulerSetAllowlist(List<KeyValuePair<string, string>> sent)
    {
        if (sent.Any(a => !SchedulerSetAllowedKeys.Contains(a.Key)))
        {
            throw new DomainInvariantException("scheduler/set allows only .id and disabled=yes.");
        }

        if (!sent.Any(static a => a.Key == "disabled" && string.Equals(a.Value, "yes", StringComparison.Ordinal)))
        {
            throw new DomainInvariantException("scheduler/set must set disabled=yes.");
        }
    }

    private static void EnsureReadBack(
        IReadOnlyDictionary<string, string> readBack,
        IReadOnlyList<KeyValuePair<string, string>> expected)
    {
        foreach ((string key, string value) in expected)
        {
            if (key is ".id")
            {
                continue;
            }

            if (!string.Equals(readBack.GetValueOrDefault(key), value, StringComparison.Ordinal)
                && !(key == "disabled" && YesEquals(readBack.GetValueOrDefault(key), value)))
            {
                throw new InvalidOperationException($"Read-back mismatch for '{key}'.");
            }
        }
    }

    private static bool MatchesFilterIdentity(IReadOnlyDictionary<string, string> row, FilterRuleWrite write)
    {
        if (!string.Equals(row.GetValueOrDefault("chain"), write.Chain, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(row.GetValueOrDefault("action"), write.Action, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (write.Comment is not null
            && !string.Equals(row.GetValueOrDefault("comment"), write.Comment, StringComparison.Ordinal))
        {
            return false;
        }

        if (write.JumpTarget is not null
            && !string.Equals(row.GetValueOrDefault("jump-target"), write.JumpTarget, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string BuiltinChainName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported chain '{chain}'."),
        };

    private static RouterOsItemId? TrySessionId(IReadOnlyDictionary<string, string> row)
        => row.TryGetValue(".id", out string? id) && !string.IsNullOrWhiteSpace(id)
            ? RouterOsItemId.Create(id)
            : null;

    private static bool Yes(string? value)
        => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool YesEquals(string? actual, string expected)
        => Yes(actual) == Yes(expected);

    private static DeploymentWriteExecutionResult Ok(
        DeploymentWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> sent,
        IReadOnlyDictionary<string, string> readBack,
        RouterOsItemId? sessionItemId)
        => new()
        {
            Succeeded = true,
            Path = DeploymentWritePaths.Fixed(path),
            SentAttributes = sent,
            ReadBack = readBack,
            SessionItemId = sessionItemId,
        };

    private static DeploymentWriteExecutionResult Fail(
        DeploymentWritePath path,
        IReadOnlyList<KeyValuePair<string, string>> sent,
        string error)
        => new()
        {
            Succeeded = false,
            Path = DeploymentWritePaths.Fixed(path),
            SentAttributes = sent,
            ReadBack = new Dictionary<string, string>(StringComparer.Ordinal),
            Error = error,
        };

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
