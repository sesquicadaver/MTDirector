using Mfc.Application.Abstractions.RouterOs;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Maps allowlisted <c>/ip/neighbor/print</c> results into application neighbor rows (#314).
/// Distinct from M7.2 <c>/ipv6/neighbor</c> endpoint attribution.
/// </summary>
public static class NeighborDiscoveryMapper
{
    public const ushort SuggestedApiSslPort = 8729;

    public static IReadOnlyList<RouterOsNeighborRow> MapRows(RosReadCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        List<RouterOsNeighborRow> rows = [];
        foreach (RosReadRecord row in result.Records)
        {
            Dictionary<string, string> known = row.KnownProperties
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            rows.Add(new RouterOsNeighborRow
            {
                Address = TrimOrNull(Get(known, "address")),
                MacAddress = TrimOrNull(Get(known, "mac-address")),
                Identity = TrimOrNull(Get(known, "identity")),
                Platform = TrimOrNull(Get(known, "platform")),
                Version = TrimOrNull(Get(known, "version")),
                Board = TrimOrNull(Get(known, "board")),
                Interface = TrimOrNull(Get(known, "interface")),
                Age = TrimOrNull(Get(known, "age")),
            });
        }

        return rows;
    }

    public static string ReadSeedIdentity(RosReadCommandResult identityResult)
    {
        ArgumentNullException.ThrowIfNull(identityResult);
        foreach (RosReadRecord row in identityResult.Records)
        {
            if (row.KnownProperties.TryGetValue("name", out string? name)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }

        return string.Empty;
    }

    private static string? Get(Dictionary<string, string> known, string name)
        => known.TryGetValue(name, out string? value) ? value : null;

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
