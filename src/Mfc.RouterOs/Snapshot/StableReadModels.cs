using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Outcome of a stable-read coordination attempt sequence (M1-19).</summary>
public enum StableReadOutcome : byte
{
    /// <summary>Fingerprints matched; discovery dataset may be treated as complete.</summary>
    Accepted = 0,

    /// <summary>Configuration changed across all bounded attempts — never persist as complete.</summary>
    SnapshotUnstable = 1,

    /// <summary>Caller cancelled before acceptance.</summary>
    Canceled = 2,
}

/// <summary>Critical configuration menus that participate in stable-read fingerprints (MVP §8.2).</summary>
public enum CriticalConfigurationMenu : byte
{
    Filter = 1,
    AddressList = 2,
    InterfaceList = 3,
    Vrrp = 4,
    RoutesAndRoutingRules = 5,
    Nat = 6,
    Mangle = 7,
    Raw = 8,
    IpServices = 9,

    /// <summary>Managed anchors — optional until onboarding menus exist; must match across passes.</summary>
    ManagedAnchors = 10,
}

/// <summary>Per-menu configuration fingerprint (config material only — never runtime observations).</summary>
public sealed record MenuFingerprint
{
    public required CriticalConfigurationMenu Menu { get; init; }

    public required Hash256 Digest { get; init; }

    /// <summary>False when the menu is unavailable on this device (must be equal across both fingerprint passes).</summary>
    public required bool Available { get; init; }
}

/// <summary>Ordered set of critical-menu fingerprints used for stable-read compare.</summary>
public sealed class ConfigurationFingerprintSet : IEquatable<ConfigurationFingerprintSet>
{
    public ConfigurationFingerprintSet(IReadOnlyList<MenuFingerprint> menus)
    {
        ArgumentNullException.ThrowIfNull(menus);
        Menus = menus;
        AggregateDigest = ComputeAggregate(menus);
    }

    public IReadOnlyList<MenuFingerprint> Menus { get; }

    public Hash256 AggregateDigest { get; }

    public bool Equals(ConfigurationFingerprintSet? other)
    {
        if (other is null || Menus.Count != other.Menus.Count)
        {
            return false;
        }

        for (int i = 0; i < Menus.Count; i++)
        {
            MenuFingerprint left = Menus[i];
            MenuFingerprint right = other.Menus[i];
            if (left.Menu != right.Menu
                || left.Available != right.Available
                || !left.Digest.Equals(right.Digest))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ConfigurationFingerprintSet);

    public override int GetHashCode() => AggregateDigest.GetHashCode();

    private static Hash256 ComputeAggregate(IReadOnlyList<MenuFingerprint> menus)
    {
        using System.Security.Cryptography.IncrementalHash hasher =
            System.Security.Cryptography.IncrementalHash.CreateHash(
                System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (MenuFingerprint menu in menus)
        {
            hasher.AppendData(System.Text.Encoding.UTF8.GetBytes(menu.Menu.ToString()));
            hasher.AppendData([(byte)(menu.Available ? 1 : 0)]);
            hasher.AppendData(menu.Digest.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }
}

/// <summary>Limits for one stable-read coordination (Vertical Slice §10.5 / §19.2).</summary>
public sealed class StableReadOptions
{
    public const int DefaultMaxAttempts = 3;
    public const int DefaultMaxParallelCommands = 8;
    public static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultFullCaptureTimeout = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan DefaultRetryDelayMin = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan DefaultRetryDelayMax = TimeSpan.FromSeconds(2);

    public int MaxAttempts { get; init; } = DefaultMaxAttempts;

    public int MaxParallelCommands { get; init; } = DefaultMaxParallelCommands;

    public TimeSpan CommandTimeout { get; init; } = DefaultCommandTimeout;

    public TimeSpan FullCaptureTimeout { get; init; } = DefaultFullCaptureTimeout;

    public TimeSpan RetryDelayMin { get; init; } = DefaultRetryDelayMin;

    public TimeSpan RetryDelayMax { get; init; } = DefaultRetryDelayMax;

    public void Validate()
    {
        if (MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "MaxAttempts must be >= 1.");
        }

        if (MaxParallelCommands < 1 || MaxParallelCommands > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxParallelCommands),
                "MaxParallelCommands must be in [1, 8].");
        }

        if (CommandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CommandTimeout));
        }

        if (FullCaptureTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FullCaptureTimeout));
        }

        if (RetryDelayMin < TimeSpan.Zero || RetryDelayMax < RetryDelayMin)
        {
            throw new ArgumentOutOfRangeException(nameof(RetryDelayMax), "Retry delay bounds are invalid.");
        }
    }
}

/// <summary>Execution context passed into discovery reads (timeouts + bounded concurrency).</summary>
public sealed class StableReadExecutionContext
{
    public StableReadExecutionContext(StableReadOptions options, BoundedCommandParallelism parallelism)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(parallelism);
        Options = options;
        Parallelism = parallelism;
    }

    public StableReadOptions Options { get; }

    public BoundedCommandParallelism Parallelism { get; }

    public TimeSpan CommandTimeout => Options.CommandTimeout;
}

/// <summary>Result of stable-read coordination.</summary>
/// <typeparam name="TDataset">Opaque discovery dataset type (assembled further in M1-20).</typeparam>
public sealed class StableReadResult<TDataset>
{
    public required StableReadOutcome Outcome { get; init; }

    public TDataset? Dataset { get; init; }

    public ConfigurationFingerprintSet? AcceptedFingerprints { get; init; }

    public required int AttemptsUsed { get; init; }

    /// <summary>True only when Outcome is Accepted — partial/unstable results must never be stored as complete.</summary>
    public bool IsComplete => Outcome == StableReadOutcome.Accepted && Dataset is not null;
}

/// <summary>
/// One capture attempt session: fingerprints → discovery → fingerprints.
/// Implementations must expose only read operations (no RouterOS writes).
/// </summary>
/// <typeparam name="TDataset">Discovery dataset produced for this attempt.</typeparam>
public interface IStableReadAttemptSession<TDataset> : IAsyncDisposable
{
    Task<ConfigurationFingerprintSet> ReadConfigurationFingerprintsAsync(
        StableReadExecutionContext context,
        CancellationToken cancellationToken);

    Task<TDataset> ReadCompleteDiscoveryDatasetAsync(
        StableReadExecutionContext context,
        CancellationToken cancellationToken);
}

/// <summary>Opens a fresh attempt session (new connection) for each retry (Read Adapter §13.3).</summary>
/// <typeparam name="TDataset">Discovery dataset type.</typeparam>
public interface IStableReadAttemptFactory<TDataset>
{
    Task<IStableReadAttemptSession<TDataset>> OpenAsync(CancellationToken cancellationToken);
}

/// <summary>Bounded retry delay with jitter (tests inject a fake).</summary>
public interface IStableReadDelay
{
    Task DelayAsync(TimeSpan min, TimeSpan max, CancellationToken cancellationToken);
}

/// <summary>Production delay using <see cref="Random"/> jitter within [min, max].</summary>
public sealed class JitterStableReadDelay : IStableReadDelay
{
    public Task DelayAsync(TimeSpan min, TimeSpan max, CancellationToken cancellationToken)
    {
        if (max <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        double minMs = min.TotalMilliseconds;
        double maxMs = max.TotalMilliseconds;
        double chosen = minMs + (Random.Shared.NextDouble() * (maxMs - minMs));
        return Task.Delay(TimeSpan.FromMilliseconds(chosen), cancellationToken);
    }
}

/// <summary>Maps critical menus to allowlisted read command ids (config-bearing; not observation-only).</summary>
public static class CriticalConfigurationMenus
{
    /// <summary>All menus that must appear in every fingerprint set, in stable order.</summary>
    public static IReadOnlyList<CriticalConfigurationMenu> All { get; } =
    [
        CriticalConfigurationMenu.Filter,
        CriticalConfigurationMenu.AddressList,
        CriticalConfigurationMenu.InterfaceList,
        CriticalConfigurationMenu.Vrrp,
        CriticalConfigurationMenu.RoutesAndRoutingRules,
        CriticalConfigurationMenu.Nat,
        CriticalConfigurationMenu.Mangle,
        CriticalConfigurationMenu.Raw,
        CriticalConfigurationMenu.IpServices,
        CriticalConfigurationMenu.ManagedAnchors,
    ];

    /// <summary>
    /// Allowlisted read commands contributing configuration material for fingerprints.
    /// Observation-only commands (default-route state) are intentionally excluded.
    /// </summary>
    public static IReadOnlyList<RosReadCommandId> CommandsFor(CriticalConfigurationMenu menu)
        => menu switch
        {
            CriticalConfigurationMenu.Filter => [RosReadCommandId.Ipv4Filter, RosReadCommandId.Ipv6Filter],
            CriticalConfigurationMenu.AddressList =>
                [RosReadCommandId.Ipv4AddressLists, RosReadCommandId.Ipv6AddressLists],
            CriticalConfigurationMenu.InterfaceList =>
                [RosReadCommandId.InterfaceLists, RosReadCommandId.InterfaceListMembers],
            CriticalConfigurationMenu.Vrrp => [RosReadCommandId.VrrpInterfaces],
            CriticalConfigurationMenu.RoutesAndRoutingRules =>
            [
                RosReadCommandId.RoutingTables,
                RosReadCommandId.RoutingRules,
                RosReadCommandId.Ipv4StaticRoutes,
                RosReadCommandId.Ipv6StaticRoutes,
                RosReadCommandId.Ipv4Settings,
                RosReadCommandId.Ipv6Settings,
            ],
            CriticalConfigurationMenu.Nat => [RosReadCommandId.Ipv4Nat, RosReadCommandId.Ipv6Nat],
            CriticalConfigurationMenu.Mangle => [RosReadCommandId.Ipv4Mangle, RosReadCommandId.Ipv6Mangle],
            CriticalConfigurationMenu.Raw => [RosReadCommandId.Ipv4Raw, RosReadCommandId.Ipv6Raw],
            CriticalConfigurationMenu.IpServices => [RosReadCommandId.IpServices],
            CriticalConfigurationMenu.ManagedAnchors => [],
            _ => throw new ArgumentOutOfRangeException(nameof(menu)),
        };
}
