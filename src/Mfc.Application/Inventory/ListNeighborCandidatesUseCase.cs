using System.Net;
using System.Net.Sockets;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Inventory;

public sealed class ListNeighborCandidatesCommand
{
    public required string Actor { get; init; }

    public required Guid SeedDeviceId { get; init; }
}

/// <summary>
/// On-demand MikroTik neighbor suggestions from a registered seed device (#314).
/// Reads allowlisted <c>/ip/neighbor</c> via Controller; never auto-registers.
/// </summary>
public sealed class ListNeighborCandidatesUseCase
{
    public const ushort SuggestedManagementPort = 8729;

    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsReadPort _routerOs;

    public ListNeighborCandidatesUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        IRouterOsReadPort routerOs)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(routerOs);
        _auth = auth;
        _devices = devices;
        _profiles = profiles;
        _routerOs = routerOs;
    }

    public async Task<ApplicationResult<NeighborCandidatesView>> ExecuteAsync(
        ListNeighborCandidatesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.DiscoveryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Device? seed = await _devices.GetAsync(new DeviceId(command.SeedDeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (seed is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound($"Device '{command.SeedDeviceId}' not found."));
        }

        ConnectionProfileReadModel? profile = await _profiles.GetAsync(seed.Id, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Failed($"Connection profile for device '{command.SeedDeviceId}' is missing."));
        }

        RouterOsReadTarget target = new()
        {
            DeviceId = seed.Id,
            Endpoint = seed.ManagementEndpoint,
            SecretReference = profile.SecretReference,
            TrustMode = profile.TrustMode,
            CaProfileRef = profile.CaProfileRef,
            PinnedSpkiSha256 = profile.PinnedSpkiSha256,
        };

        RouterOsNeighborDiscoveryResult discovery;
        try
        {
            discovery = await _routerOs.ListNeighborRowsAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Failed(ex.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ApplicationResults.Fail(
                ApplicationError.Dependency("RouterOS neighbor read failed (sanitized)."));
        }

        IReadOnlyList<Device> nodeDevices = await _devices
            .ListByNodeAsync(seed.NodeId, cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> knownHosts = new(StringComparer.OrdinalIgnoreCase);
        foreach (Device device in nodeDevices)
        {
            knownHosts.Add(device.ManagementEndpoint.Host.Value);
        }

        knownHosts.Add(seed.ManagementEndpoint.Host.Value);

        IReadOnlyList<NeighborCandidateView> candidates = NeighborCandidateFilter.SelectMikroTikCandidates(
            discovery.Rows,
            discovery.SeedIdentity,
            knownHosts,
            SuggestedManagementPort,
            seed.ManagementEndpoint.Host.Value);

        return ApplicationResults.Ok(new NeighborCandidatesView
        {
            SeedDeviceId = seed.Id.Value,
            SeedIdentity = discovery.SeedIdentity,
            Candidates = candidates,
            RouterOsMutated = false,
        });
    }
}

/// <summary>Pure MikroTik filter + address/identity dedup for seed neighbor suggestions (#314).</summary>
public static class NeighborCandidateFilter
{
    public static bool IsMikroTikPlatform(string? platform)
        => !string.IsNullOrWhiteSpace(platform)
           && platform.Contains("MikroTik", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Filters MikroTik MNDP rows into one suggestion per identity (or per address when identity is empty).
    /// Multi-homed neighbors (mgmt + LAN + VRRP VIP) collapse to a single row; prefers the address in the
    /// same IPv4 /24 as <paramref name="seedManagementHost"/> when available.
    /// </summary>
    public static IReadOnlyList<NeighborCandidateView> SelectMikroTikCandidates(
        IReadOnlyList<RouterOsNeighborRow> rows,
        string? seedIdentity,
        IReadOnlySet<string> knownManagementHosts,
        ushort suggestedPort = ListNeighborCandidatesUseCase.SuggestedManagementPort,
        string? seedManagementHost = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(knownManagementHosts);

        // Preserve first-seen order while collapsing duplicates by identity (or address).
        // Also enforce one suggestion per management address (CIDR-normalized).
        List<string> orderKeys = [];
        Dictionary<string, ScoredCandidate> byKey = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> addressOwnerKey = new(StringComparer.OrdinalIgnoreCase);
        int sequence = 0;

        foreach (RouterOsNeighborRow row in rows)
        {
            if (!IsMikroTikPlatform(row.Platform))
            {
                continue;
            }

            string? address = NormalizeAddress(row.Address);
            if (address is null)
            {
                continue;
            }

            if (knownManagementHosts.Contains(address))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(seedIdentity)
                && !string.IsNullOrWhiteSpace(row.Identity)
                && string.Equals(seedIdentity.Trim(), row.Identity.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string key = DedupKey(row.Identity, address);
            int score = PreferenceScore(address, seedManagementHost);
            NeighborCandidateView view = new()
            {
                Address = address,
                SuggestedPort = suggestedPort,
                Identity = row.Identity,
                MacAddress = row.MacAddress,
                Platform = row.Platform,
                Version = row.Version,
                Board = row.Board,
                Interface = row.Interface,
                Age = row.Age,
            };

            if (!byKey.TryGetValue(key, out ScoredCandidate? existing))
            {
                if (addressOwnerKey.ContainsKey(address))
                {
                    // Another identity already claimed this host — skip duplicate address row.
                    continue;
                }

                orderKeys.Add(key);
                byKey[key] = new ScoredCandidate(view, score, sequence++);
                addressOwnerKey[address] = key;
                continue;
            }

            // Higher score wins; on ties keep the earlier MNDP row (stable).
            if (score <= existing.Score)
            {
                continue;
            }

            if (!string.Equals(address, existing.View.Address, StringComparison.OrdinalIgnoreCase)
                && addressOwnerKey.ContainsKey(address))
            {
                // Preferred address already used by a different candidate — keep current.
                continue;
            }

            addressOwnerKey.Remove(existing.View.Address);
            addressOwnerKey[address] = key;
            byKey[key] = new ScoredCandidate(view, score, existing.Sequence);
        }

        List<NeighborCandidateView> selected = new(orderKeys.Count);
        foreach (string key in orderKeys)
        {
            selected.Add(byKey[key].View);
        }

        return selected;
    }

    /// <summary>Identity when present; otherwise address (anonymous MNDP rows).</summary>
    internal static string DedupKey(string? identity, string address)
    {
        if (!string.IsNullOrWhiteSpace(identity))
        {
            return "id:" + identity.Trim();
        }

        return "addr:" + address;
    }

    /// <summary>
    /// Prefer management-plane addresses that share the seed device's IPv4 /24 (lab/prod multi-homed CHR).
    /// </summary>
    internal static int PreferenceScore(string address, string? seedManagementHost)
    {
        if (string.IsNullOrWhiteSpace(seedManagementHost))
        {
            return 0;
        }

        if (!TryParseIpv4(address, out byte[] candidateOctets)
            || !TryParseIpv4(seedManagementHost.Trim(), out byte[] seedOctets))
        {
            return 0;
        }

        if (candidateOctets[0] == seedOctets[0]
            && candidateOctets[1] == seedOctets[1]
            && candidateOctets[2] == seedOctets[2])
        {
            return 100;
        }

        if (candidateOctets[0] == seedOctets[0]
            && candidateOctets[1] == seedOctets[1])
        {
            return 10;
        }

        return 0;
    }

    private static bool TryParseIpv4(string host, out byte[] octets)
    {
        octets = [];
        if (!IPAddress.TryParse(host, out IPAddress? parsed)
            || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        octets = parsed.GetAddressBytes();
        return octets.Length == 4;
    }

    private static string? NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        string trimmed = address.Trim();
        // RouterOS may emit address/CIDR; keep host part only.
        int slash = trimmed.IndexOf('/');
        if (slash > 0)
        {
            trimmed = trimmed[..slash];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ScoredCandidate(NeighborCandidateView View, int Score, int Sequence);
}
