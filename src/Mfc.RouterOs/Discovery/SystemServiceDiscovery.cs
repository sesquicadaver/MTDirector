using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Discovery;

/// <summary>
/// Reads system identity, resource/capability, packages, clock, and API-SSL service metadata
/// via the typed allowlisted executor only (M1-11). Never uses /export or show-sensitive.
/// </summary>
public static class SystemServiceDiscovery
{
    private static readonly RosReadCommandId[] RequiredCommands =
    [
        RosReadCommandId.SystemIdentity,
        RosReadCommandId.SystemResource,
        RosReadCommandId.SystemPackages,
        RosReadCommandId.SystemClock,
        RosReadCommandId.IpServices,
    ];

    /// <summary>Discovers system and management-service metadata from an open session.</summary>
    public static async Task<SystemServiceDiscoveryResult> DiscoverAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        List<string> warnings = [];
        RosReadCommandResult identity = await ExecuteRequiredAsync(
            session, RosReadCommandId.SystemIdentity, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult resource = await ExecuteRequiredAsync(
            session, RosReadCommandId.SystemResource, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult packages = await ExecuteRequiredAsync(
            session, RosReadCommandId.SystemPackages, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult clock = await ExecuteRequiredAsync(
            session, RosReadCommandId.SystemClock, warnings, cancellationToken).ConfigureAwait(false);
        RosReadCommandResult services = await ExecuteRequiredAsync(
            session, RosReadCommandId.IpServices, warnings, cancellationToken).ConfigureAwait(false);

        RosReadCommandResult routerboardResult = await RosReadCommandExecutor.ExecuteAsync(
            session,
            RosReadCommandId.SystemRouterboard,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return BuildResult(identity, resource, packages, clock, services, routerboardResult, warnings);
    }

    /// <summary>Builds discovery from already-executed command results (unit-testable / capture reader).</summary>
    public static SystemServiceDiscoveryResult BuildResult(
        RosReadCommandResult identity,
        RosReadCommandResult resource,
        RosReadCommandResult packages,
        RosReadCommandResult clock,
        RosReadCommandResult services,
        RosReadCommandResult routerboardResult,
        IReadOnlyList<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(routerboardResult);

        List<string> effectiveWarnings = warnings is null ? [] : [..warnings];
        SystemRouterboardDiscovery routerboard;
        if (routerboardResult.IsSuccess)
        {
            routerboard = MapRouterboard(routerboardResult, available: true);
        }
        else
        {
            effectiveWarnings.Add(
                $"SystemRouterboard unavailable: {routerboardResult.Error?.Code} {routerboardResult.Error?.Message}");
            routerboard = new SystemRouterboardDiscovery
            {
                Available = false,
                Routerboard = null,
                Model = null,
                SerialNumber = null,
                FirmwareType = null,
                FactoryFirmware = null,
                CurrentFirmware = null,
                UpgradeFirmware = null,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            };
        }

        return new SystemServiceDiscoveryResult
        {
            Identity = MapIdentity(identity),
            Resource = MapResource(resource),
            Routerboard = routerboard,
            Packages = MapPackages(packages),
            Clock = MapClock(clock),
            ApiSsl = MapApiSsl(services),
            Warnings = effectiveWarnings,
        };
    }

    /// <summary>True when the command id is part of M1-11 system/service discovery.</summary>
    public static bool IsSystemServiceCommand(RosReadCommandId id)
        => RequiredCommands.Contains(id) || id == RosReadCommandId.SystemRouterboard;

    private static async Task<RosReadCommandResult> ExecuteRequiredAsync(
        RosSession session,
        RosReadCommandId commandId,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            session,
            commandId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            warnings.Add($"{commandId}: {result.Error?.Code} {result.Error?.Message}");
        }

        return result;
    }

    private static SystemIdentityDiscovery MapIdentity(RosReadCommandResult result)
    {
        RosReadRecord row = FirstOrEmpty(result);
        return new SystemIdentityDiscovery
        {
            Name = Get(row, "name"),
            RawProperties = row.RawProperties,
        };
    }

    private static SystemResourceDiscovery MapResource(RosReadCommandResult result)
    {
        RosReadRecord row = FirstOrEmpty(result);
        return new SystemResourceDiscovery
        {
            Version = Get(row, "version"),
            BuildTime = Get(row, "build-time"),
            ArchitectureName = Get(row, "architecture-name"),
            BoardName = Get(row, "board-name"),
            Platform = Get(row, "platform"),
            Uptime = Get(row, "uptime"),
            RawProperties = row.RawProperties,
        };
    }

    private static SystemRouterboardDiscovery MapRouterboard(RosReadCommandResult result, bool available)
    {
        RosReadRecord row = FirstOrEmpty(result);
        return new SystemRouterboardDiscovery
        {
            Available = available,
            Routerboard = Get(row, "routerboard"),
            Model = Get(row, "model"),
            SerialNumber = Get(row, "serial-number"),
            FirmwareType = Get(row, "firmware-type"),
            FactoryFirmware = Get(row, "factory-firmware"),
            CurrentFirmware = Get(row, "current-firmware"),
            UpgradeFirmware = Get(row, "upgrade-firmware"),
            RawProperties = row.RawProperties,
        };
    }

    private static List<SystemPackageDiscovery> MapPackages(RosReadCommandResult result)
    {
        List<SystemPackageDiscovery> packages = new(result.Records.Count);
        foreach (RosReadRecord row in result.Records)
        {
            packages.Add(new SystemPackageDiscovery
            {
                Id = Get(row, ".id"),
                Name = Get(row, "name"),
                Version = Get(row, "version"),
                BuildTime = Get(row, "build-time"),
                Scheduled = Get(row, "scheduled"),
                Disabled = Get(row, "disabled"),
                RawProperties = row.RawProperties,
            });
        }

        return packages;
    }

    private static SystemClockDiscovery MapClock(RosReadCommandResult result)
    {
        RosReadRecord row = FirstOrEmpty(result);
        return new SystemClockDiscovery
        {
            Time = Get(row, "time"),
            Date = Get(row, "date"),
            TimeZoneName = Get(row, "time-zone-name"),
            GmtOffset = Get(row, "gmt-offset"),
            DstActive = Get(row, "dst-active"),
            RawProperties = row.RawProperties,
        };
    }

    private static ApiSslServiceDiscovery MapApiSsl(RosReadCommandResult result)
    {
        foreach (RosReadRecord row in result.Records)
        {
            string? name = Get(row, "name");
            if (!string.Equals(name, "api-ssl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? disabled = Get(row, "disabled");
            return new ApiSslServiceDiscovery
            {
                Found = true,
                Disabled = string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(disabled, "yes", StringComparison.OrdinalIgnoreCase),
                Port = Get(row, "port"),
                AddressPrefixes = Get(row, "address"),
                Certificate = Get(row, "certificate"),
                TlsVersion = Get(row, "tls-version"),
                Vrf = Get(row, "vrf"),
                RawProperties = row.RawProperties,
            };
        }

        return new ApiSslServiceDiscovery
        {
            Found = false,
            Disabled = true,
            Port = null,
            AddressPrefixes = null,
            Certificate = null,
            TlsVersion = null,
            Vrf = null,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static RosReadRecord FirstOrEmpty(RosReadCommandResult result)
    {
        if (result.Records.Count > 0)
        {
            return result.Records[0];
        }

        return new RosReadRecord
        {
            KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static string? Get(RosReadRecord row, string name)
        => row.KnownProperties.TryGetValue(name, out string? value) ? value : null;
}
