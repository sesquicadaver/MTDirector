namespace Mfc.RouterOs.Discovery;

/// <summary>Typed system identity from <c>/system/identity/print</c>.</summary>
public sealed class SystemIdentityDiscovery
{
    public required string? Name { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>
/// System resource / capability fields. <see cref="Uptime"/> is observation-only
/// and must never enter a configuration hash.
/// </summary>
public sealed class SystemResourceDiscovery
{
    public required string? Version { get; init; }

    public required string? BuildTime { get; init; }

    public required string? ArchitectureName { get; init; }

    public required string? BoardName { get; init; }

    public required string? Platform { get; init; }

    /// <summary>Runtime uptime. Excluded from configuration hash material.</summary>
    public required string? Uptime { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Optional routerboard metadata (conditional on hardware).</summary>
public sealed class SystemRouterboardDiscovery
{
    public required bool Available { get; init; }

    public required string? Routerboard { get; init; }

    public required string? Model { get; init; }

    public required string? SerialNumber { get; init; }

    public required string? FirmwareType { get; init; }

    public required string? FactoryFirmware { get; init; }

    public required string? CurrentFirmware { get; init; }

    public required string? UpgradeFirmware { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

public sealed class SystemPackageDiscovery
{
    public required string? Id { get; init; }

    public required string? Name { get; init; }

    public required string? Version { get; init; }

    public required string? BuildTime { get; init; }

    public required string? Scheduled { get; init; }

    public required string? Disabled { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Clock observation + configured timezone.</summary>
public sealed class SystemClockDiscovery
{
    public required string? Time { get; init; }

    public required string? Date { get; init; }

    public required string? TimeZoneName { get; init; }

    public required string? GmtOffset { get; init; }

    public required string? DstActive { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>API-SSL management service state for validators.</summary>
public sealed class ApiSslServiceDiscovery
{
    public required bool Found { get; init; }

    public required bool Disabled { get; init; }

    public required string? Port { get; init; }

    public required string? AddressPrefixes { get; init; }

    public required string? Certificate { get; init; }

    public required string? TlsVersion { get; init; }

    public required string? Vrf { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Aggregate system + management-service discovery result (M1-11).</summary>
public sealed class SystemServiceDiscoveryResult
{
    public required SystemIdentityDiscovery Identity { get; init; }

    public required SystemResourceDiscovery Resource { get; init; }

    public required SystemRouterboardDiscovery Routerboard { get; init; }

    public required IReadOnlyList<SystemPackageDiscovery> Packages { get; init; }

    public required SystemClockDiscovery Clock { get; init; }

    public required ApiSslServiceDiscovery ApiSsl { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Stable key/value material for configuration hashing.
    /// Excludes runtime uptime and other observation-only fields.
    /// </summary>
    public IReadOnlyDictionary<string, string> ConfigurationHashMaterial
    {
        get
        {
            Dictionary<string, string> material = new(StringComparer.Ordinal);
            Put(material, "identity.name", Identity.Name);
            Put(material, "resource.version", Resource.Version);
            Put(material, "resource.build-time", Resource.BuildTime);
            Put(material, "resource.architecture-name", Resource.ArchitectureName);
            Put(material, "resource.board-name", Resource.BoardName);
            Put(material, "resource.platform", Resource.Platform);
            // Intentionally omit Resource.Uptime.
            if (Routerboard.Available)
            {
                Put(material, "routerboard.model", Routerboard.Model);
                Put(material, "routerboard.serial-number", Routerboard.SerialNumber);
                Put(material, "routerboard.current-firmware", Routerboard.CurrentFirmware);
            }

            Put(material, "clock.time-zone-name", Clock.TimeZoneName);
            Put(material, "api-ssl.disabled", ApiSsl.Found ? (ApiSsl.Disabled ? "true" : "false") : null);
            Put(material, "api-ssl.port", ApiSsl.Port);
            Put(material, "api-ssl.address", ApiSsl.AddressPrefixes);
            Put(material, "api-ssl.certificate", ApiSsl.Certificate);
            Put(material, "api-ssl.tls-version", ApiSsl.TlsVersion);

            foreach (SystemPackageDiscovery package in Packages.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                string key = package.Name ?? package.Id ?? "unknown";
                Put(material, $"package.{key}.version", package.Version);
                Put(material, $"package.{key}.disabled", package.Disabled);
            }

            return material;
        }
    }

    private static void Put(Dictionary<string, string> target, string key, string? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }
}
