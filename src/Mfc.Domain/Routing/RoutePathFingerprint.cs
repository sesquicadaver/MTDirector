using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mfc.Domain.Routing;

/// <summary>
/// Stable fingerprint of prefix + next hops + egress + execution path from a route trace (M7.1 Spec §13).
/// </summary>
public sealed class RoutePathFingerprint : IEquatable<RoutePathFingerprint>
{
    public string? MatchedPrefix { get; init; }

    public IReadOnlyList<string> NextHops { get; init; } = [];

    public IReadOnlyList<string> EgressInterfaces { get; init; } = [];

    public string? ExecutionPath { get; init; }

    /// <summary>Builds a fingerprint from a resolved route trace.</summary>
    public static RoutePathFingerprint FromTrace(RouteResolutionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        return new RoutePathFingerprint
        {
            MatchedPrefix = Normalize(trace.MatchedPrefix),
            NextHops = CollectNextHops(trace),
            EgressInterfaces = NormalizeOrdered(trace.EgressInterfaces),
            ExecutionPath = Normalize(trace.ExecutionPath),
        };
    }

    /// <summary>True when any fingerprint dimension differs from the baseline.</summary>
    public static bool PathChanged(RoutePathFingerprint? baseline, RoutePathFingerprint current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (baseline is null)
        {
            return false;
        }

        return !baseline.Equals(current);
    }

    public bool Equals(RoutePathFingerprint? other)
    {
        if (other is null)
        {
            return false;
        }

        if (!string.Equals(Normalize(MatchedPrefix), Normalize(other.MatchedPrefix), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(Normalize(ExecutionPath), Normalize(other.ExecutionPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SetEquals(NextHops, other.NextHops) && SetEquals(EgressInterfaces, other.EgressInterfaces);
    }

    public override bool Equals(object? obj) => obj is RoutePathFingerprint other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            Normalize(MatchedPrefix)?.ToUpperInvariant(),
            HashSet(NormalizeOrdered(NextHops)),
            HashSet(NormalizeOrdered(EgressInterfaces)),
            Normalize(ExecutionPath)?.ToUpperInvariant());

    /// <summary>Deterministic digest for persistence comparisons.</summary>
    public string ToDigest()
    {
        StringBuilder builder = new();
        builder.Append(Normalize(MatchedPrefix));
        builder.Append('|');
        builder.Append(string.Join(',', NormalizeOrdered(NextHops)));
        builder.Append('|');
        builder.Append(string.Join(',', NormalizeOrdered(EgressInterfaces)));
        builder.Append('|');
        builder.Append(Normalize(ExecutionPath));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static string[] CollectNextHops(RouteResolutionTrace trace)
    {
        HashSet<string> hops = new(StringComparer.OrdinalIgnoreCase);
        foreach (ImmediateNextHop hop in trace.ImmediateNextHops)
        {
            AddGateway(hops, hop.Gateway);
        }

        foreach (SelectedRoute route in trace.SelectedRoutes)
        {
            AddGateway(hops, route.Gateway);
            AddGateway(hops, ParseImmediateGateway(route.ImmediateGateway));
        }

        if (trace.EcmpRouteSet is not null)
        {
            foreach (EcmpNextHop hop in trace.EcmpRouteSet.ActiveNextHops)
            {
                AddGateway(hops, hop.Gateway);
            }
        }

        return hops.OrderBy(static h => h, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddGateway(HashSet<string> hops, string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return;
        }

        hops.Add(NormalizeGateway(gateway));
    }

    private static string NormalizeGateway(string gateway)
    {
        string trimmed = gateway.Trim();
        int percent = trimmed.IndexOf('%', StringComparison.Ordinal);
        return percent >= 0 ? trimmed[..percent] : trimmed;
    }

    private static string? ParseImmediateGateway(string? immediateGateway)
    {
        if (string.IsNullOrWhiteSpace(immediateGateway))
        {
            return null;
        }

        return NormalizeGateway(immediateGateway);
    }

    private static bool SetEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
        => HashSet(NormalizeOrdered(left)).SetEquals(HashSet(NormalizeOrdered(right)));

    private static HashSet<string> HashSet(IReadOnlyList<string> values)
    {
        HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            set.Add(value.Trim());
        }

        return set;
    }

    private static string[] NormalizeOrdered(IReadOnlyList<string> values)
        => values
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Select(static v => v.Trim())
            .OrderBy(static v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
