using System.Security.Cryptography;
using System.Text;
using Mfc.Domain;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Capabilities;

/// <summary>
/// Builds a deterministic <see cref="CapabilityProfile"/> from system discovery + embedded manifest (M1-17).
/// Capability is not version-number alone: architecture, board class, menus, packages and incompatibilities apply.
/// </summary>
public static class CapabilityProfileEvaluator
{
    public static CapabilityEvaluationResult Evaluate(SystemServiceDiscoveryResult discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        CompatibilityManifestDocument manifest = CompatibilityManifestLoader.Load();
        Hash256 manifestHash = CompatibilityManifestLoader.ManifestHash;
        List<string> findings = [];

        if (!TryParseVersion(discovery.Resource.Version, out RouterOsVersion? version) || version is null)
        {
            findings.Add("VERSION_UNPARSEABLE");
            CapabilityProfile unknown = BuildProfile(
                RouterOsVersion.Create(0, 0, 0),
                discovery,
                BoardClass.Unknown,
                SupportState.NeedsRevalidation,
                manifestHash,
                findings);
            return Finish(unknown, findings, BoardClass.Unknown, manifest);
        }

        BoardClass boardClass = ClassifyBoard(
            discovery.Resource.BoardName,
            discovery.Routerboard.Model,
            discovery.Resource.Platform);
        if (boardClass == BoardClass.Unknown)
        {
            findings.Add("BOARD_CLASS_UNKNOWN");
        }

        SupportState support = ResolveSupportState(version, discovery, manifest, boardClass, findings);
        CapabilityProfile profile = BuildProfile(version, discovery, boardClass, support, manifestHash, findings);
        return Finish(profile, findings, boardClass, manifest);
    }

    private static CapabilityEvaluationResult Finish(
        CapabilityProfile profile,
        List<string> findings,
        BoardClass boardClass,
        CompatibilityManifestDocument manifest)
    {
        CapabilityHash hash = ComputeCapabilityHash(profile, boardClass);
        return new CapabilityEvaluationResult
        {
            Profile = profile,
            CapabilityHash = hash,
            BoardClass = boardClass,
            ManifestSchemaVersion = manifest.SchemaVersion,
            ManifestProfileId = manifest.ProfileId,
            Findings = findings.OrderBy(f => f, StringComparer.Ordinal).ToArray(),
        };
    }

    private static SupportState ResolveSupportState(
        RouterOsVersion version,
        SystemServiceDiscoveryResult discovery,
        CompatibilityManifestDocument manifest,
        BoardClass boardClass,
        List<string> findings)
    {
        // AC#5 / Spec §38.2: RouterOS 6 is read-only (legacy).
        if (version.Major <= 6)
        {
            findings.Add("ROS6_READ_ONLY");
            return SupportState.ReadOnly;
        }

        // AC#6: testing/development channel never receives write support.
        if (IsNonProductionChannel(version.Channel))
        {
            findings.Add("NON_PRODUCTION_CHANNEL_READ_ONLY");
            // Still evaluate build membership for visibility, but force read-only.
            if (!IsSupportedBuild(version, manifest))
            {
                findings.Add("VERSION_UNKNOWN_NEEDS_REVALIDATION");
            }

            return SupportState.ReadOnly;
        }

        if (version.Channel is not null
            && !manifest.AllowedChannels.Contains(version.Channel, StringComparer.OrdinalIgnoreCase))
        {
            findings.Add("CHANNEL_NOT_ALLOWED");
            return SupportState.NeedsRevalidation;
        }

        if (!IsSupportedBuild(version, manifest))
        {
            findings.Add("VERSION_UNKNOWN_NEEDS_REVALIDATION");
            return SupportState.NeedsRevalidation;
        }

        string? architecture = discovery.Resource.ArchitectureName;
        if (string.IsNullOrWhiteSpace(architecture)
            || !manifest.Architectures.Contains(architecture, StringComparer.OrdinalIgnoreCase))
        {
            findings.Add("ARCHITECTURE_UNSUPPORTED");
            return SupportState.NeedsRevalidation;
        }

        string boardClassKey = boardClass.ToString().ToLowerInvariant();
        if (boardClass == BoardClass.Unknown
            || !manifest.BoardClasses.Contains(boardClassKey, StringComparer.OrdinalIgnoreCase))
        {
            findings.Add("BOARD_CLASS_NEEDS_REVALIDATION");
            return SupportState.NeedsRevalidation;
        }

        foreach (string menu in manifest.RequiredMenus)
        {
            if (string.IsNullOrWhiteSpace(menu) || !menu.StartsWith('/'))
            {
                findings.Add("MANIFEST_MENU_INVALID");
                return SupportState.NeedsRevalidation;
            }
        }

        foreach (string property in manifest.RequiredProperties)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                findings.Add("MANIFEST_PROPERTY_INVALID");
                return SupportState.NeedsRevalidation;
            }
        }

        // Required discovery fields present.
        if (string.IsNullOrWhiteSpace(discovery.Resource.Version)
            || string.IsNullOrWhiteSpace(discovery.Resource.ArchitectureName)
            || string.IsNullOrWhiteSpace(discovery.Resource.BoardName)
            || string.IsNullOrWhiteSpace(discovery.Identity.Name))
        {
            findings.Add("REQUIRED_PROPERTIES_MISSING");
            return SupportState.NeedsRevalidation;
        }

        return SupportState.Supported;
    }

    private static bool IsSupportedBuild(RouterOsVersion version, CompatibilityManifestDocument manifest)
    {
        string numeric = $"{version.Major}.{version.Minor}.{version.Patch}";
        string compact = version.Patch == 0 ? $"{version.Major}.{version.Minor}" : numeric;
        return manifest.SupportedRouterOsBuilds.Any(b =>
            string.Equals(b, numeric, StringComparison.Ordinal)
            || string.Equals(b, compact, StringComparison.Ordinal));
    }

    private static bool IsNonProductionChannel(string? channel)
        => string.Equals(channel, "testing", StringComparison.OrdinalIgnoreCase)
           || string.Equals(channel, "development", StringComparison.OrdinalIgnoreCase);

    private static CapabilityProfile BuildProfile(
        RouterOsVersion version,
        SystemServiceDiscoveryResult discovery,
        BoardClass boardClass,
        SupportState supportState,
        Hash256 manifestHash,
        List<string> findings)
    {
        string architecture = string.IsNullOrWhiteSpace(discovery.Resource.ArchitectureName)
            ? "unknown"
            : discovery.Resource.ArchitectureName!;
        string model = FirstNonEmpty(
            discovery.Routerboard.Model,
            discovery.Resource.BoardName,
            boardClass.ToString().ToLowerInvariant()) ?? "unknown";

        IEnumerable<string> packages = discovery.Packages
            .Where(p => !IsTruthy(p.Disabled))
            .Select(p => p.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>();

        bool ipv6 = discovery.Packages.Any(p =>
            string.Equals(p.Name, "ipv6", StringComparison.OrdinalIgnoreCase) && !IsTruthy(p.Disabled));
        // VRRP/bridge are base RouterOS features on supported majors; packages alone are insufficient (AC#1).
        bool vrrp = version.Major >= 7;
        bool bridge = version.Major >= 7;
        bool apiSslCert = !string.IsNullOrWhiteSpace(discovery.ApiSsl.Certificate);

        if (!ipv6)
        {
            findings.Add("IPV6_PACKAGE_ABSENT");
        }

        return CapabilityProfile.Create(
            version,
            NonEmptyName.Create(architecture),
            NonEmptyName.Create(model),
            packages,
            ipv6Supported: ipv6,
            vrrpSupported: vrrp,
            bridgeSupported: bridge,
            apiSslCertificatePresent: apiSslCert,
            supportState,
            manifestHash);
    }

    /// <summary>Capability hash excludes runtime observations (uptime, clock time, etc.).</summary>
    public static CapabilityHash ComputeCapabilityHash(CapabilityProfile profile, BoardClass boardClass)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var lines = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = profile.Version.ToString(),
            ["architecture"] = profile.Architecture.Value,
            ["model"] = profile.Model.Value,
            ["boardClass"] = boardClass.ToString(),
            ["ipv6"] = profile.Ipv6Supported ? "1" : "0",
            ["vrrp"] = profile.VrrpSupported ? "1" : "0",
            ["bridge"] = profile.BridgeSupported ? "1" : "0",
            ["apiSslCert"] = profile.ApiSslCertificatePresent ? "1" : "0",
            ["support"] = profile.SupportState.ToString(),
            ["manifest"] = profile.CompatibilityManifestHash.ToString(),
            ["packages"] = string.Join(',', profile.Packages),
        };

        string material = string.Join('\n', lines.Select(kv => $"{kv.Key}={kv.Value}"));
        // Guard: observation-only tokens must never appear.
        if (material.Contains("uptime", StringComparison.OrdinalIgnoreCase)
            || material.Contains("free-memory", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Capability hash material unexpectedly contains observations.");
        }

        return CapabilityHash.FromBytes(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    public static BoardClass ClassifyBoard(string? boardName, string? routerboardModel, string? platform)
    {
        string haystack = string.Join(' ', boardName, routerboardModel, platform).ToUpperInvariant();
        if (haystack.Contains("CRS", StringComparison.Ordinal))
        {
            return BoardClass.Crs;
        }

        if (haystack.Contains("CHR", StringComparison.Ordinal))
        {
            return BoardClass.Chr;
        }

        if (string.IsNullOrWhiteSpace(boardName) && string.IsNullOrWhiteSpace(routerboardModel))
        {
            return BoardClass.Unknown;
        }

        return BoardClass.Router;
    }

    private static bool TryParseVersion(string? raw, out RouterOsVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            version = RouterOsVersion.Parse(raw);
            return true;
        }
        catch (Exception ex) when (ex is DomainInvariantException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>Result of capability evaluation for one device.</summary>
public sealed class CapabilityEvaluationResult
{
    public required CapabilityProfile Profile { get; init; }

    public required CapabilityHash CapabilityHash { get; init; }

    public required BoardClass BoardClass { get; init; }

    public required int ManifestSchemaVersion { get; init; }

    public required string ManifestProfileId { get; init; }

    public required IReadOnlyList<string> Findings { get; init; }

    /// <summary>True when a previously cached topology validation must be discarded.</summary>
    public bool InvalidatesTopologyValidation(CapabilityHash? previousCapabilityHash)
        => previousCapabilityHash is not { } previous || !previous.Equals(CapabilityHash);
}
