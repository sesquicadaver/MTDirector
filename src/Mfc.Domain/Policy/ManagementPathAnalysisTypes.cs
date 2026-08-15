using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Per-physical-device management access profile (Policy Model §46).
/// Analysis never skips in-band API-SSL checks when <see cref="OutOfBandIndependent"/> is true.
/// </summary>
public sealed class ManagementAccessProfile
{
    public required IReadOnlyList<AddressPrefix> ControllerSourcePrefixes { get; init; }

    public required string ManagementDestination { get; init; }

    public required ushort ApiSslPort { get; init; }

    public string? ExpectedIngressInterface { get; init; }

    public string? ExpectedEgressInterface { get; init; }

    public string? TrustProfile { get; init; }

    public required bool OutOfBandIndependent { get; init; }

    public IReadOnlyList<string> PhysicalManagementAddresses { get; init; } = [];

    public IReadOnlyList<string> VirtualManagementAddresses { get; init; } = [];

    public static ManagementAccessProfile Create(
        IReadOnlyList<AddressPrefix> controllerSourcePrefixes,
        string managementDestination,
        ushort apiSslPort,
        bool outOfBandIndependent = false,
        string? expectedIngressInterface = null,
        string? expectedEgressInterface = null,
        string? trustProfile = null,
        IReadOnlyList<string>? physicalManagementAddresses = null,
        IReadOnlyList<string>? virtualManagementAddresses = null)
    {
        ArgumentNullException.ThrowIfNull(controllerSourcePrefixes);
        if (controllerSourcePrefixes.Count == 0)
        {
            throw new DomainInvariantException("Management profile requires at least one controller source prefix.");
        }

        foreach (AddressPrefix prefix in controllerSourcePrefixes)
        {
            ArgumentNullException.ThrowIfNull(prefix);
        }

        if (string.IsNullOrWhiteSpace(managementDestination))
        {
            throw new DomainInvariantException("Management destination is required.");
        }

        if (apiSslPort == 0)
        {
            throw new DomainInvariantException("API-SSL port must be non-zero.");
        }

        return new ManagementAccessProfile
        {
            ControllerSourcePrefixes = controllerSourcePrefixes,
            ManagementDestination = managementDestination.Trim(),
            ApiSslPort = apiSslPort,
            OutOfBandIndependent = outOfBandIndependent,
            ExpectedIngressInterface = string.IsNullOrWhiteSpace(expectedIngressInterface)
                ? null
                : expectedIngressInterface.Trim(),
            ExpectedEgressInterface = string.IsNullOrWhiteSpace(expectedEgressInterface)
                ? null
                : expectedEgressInterface.Trim(),
            TrustProfile = string.IsNullOrWhiteSpace(trustProfile) ? null : trustProfile.Trim(),
            PhysicalManagementAddresses = NormalizeAddresses(physicalManagementAddresses),
            VirtualManagementAddresses = NormalizeAddresses(virtualManagementAddresses),
        };
    }

    /// <summary>Returns a copy with a different physical management destination (VRRP member).</summary>
    public ManagementAccessProfile WithDestination(string managementDestination)
        => Create(
            ControllerSourcePrefixes,
            managementDestination,
            ApiSslPort,
            OutOfBandIndependent,
            ExpectedIngressInterface,
            ExpectedEgressInterface,
            TrustProfile,
            PhysicalManagementAddresses,
            VirtualManagementAddresses);

    private static List<string> NormalizeAddresses(IReadOnlyList<string>? addresses)
    {
        if (addresses is null || addresses.Count == 0)
        {
            return [];
        }

        List<string> normalized = new(addresses.Count);
        foreach (string address in addresses)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new DomainInvariantException("Management address list entries must be non-empty.");
            }

            normalized.Add(address.Trim());
        }

        return normalized;
    }
}

/// <summary>API-SSL facts independent of RouterOs discovery types.</summary>
public sealed record ManagementIpServiceFacts
{
    public required bool Found { get; init; }

    public required bool Disabled { get; init; }

    public required string? Port { get; init; }

    public required string? AddressPrefixes { get; init; }

    public static ManagementIpServiceFacts Create(
        bool found,
        bool disabled,
        string? port,
        string? addressPrefixes)
        => new()
        {
            Found = found,
            Disabled = disabled,
            Port = string.IsNullOrWhiteSpace(port) ? null : port.Trim(),
            AddressPrefixes = string.IsNullOrWhiteSpace(addressPrefixes) ? null : addressPrefixes.Trim(),
        };
}

/// <summary>One management-path analysis finding. Subject is chain/ordinal, not a UUID.</summary>
public sealed class ManagementPathFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Chain { get; init; }

    public int? Ordinal { get; init; }

    public PolicyWitnessPacket? Witness { get; init; }
}

/// <summary>
/// Generated SYSTEM test (Policy Model §54 origin=SYSTEM). Not the M2-16 PolicyTestCase aggregate.
/// </summary>
public sealed class ManagementSystemTest
{
    public const string OriginSystem = "SYSTEM";

    public const string ExpectedAccept = "ACCEPT";

    public required string Origin { get; init; }

    public required PolicyFilterChain Chain { get; init; }

    public required string Expected { get; init; }

    public required PolicyWitnessPacket Packet { get; init; }
}

/// <summary>Outcome of <see cref="ManagementPathAnalysis.Analyze"/>.</summary>
public sealed class ManagementPathAnalysisResult
{
    public required IReadOnlyList<ManagementPathFinding> Findings { get; init; }

    /// <summary>SHA-256 of profile + API-SSL facts + filter identity (observation slot; M2-13).</summary>
    public required Hash256 ManagementPathContextHash { get; init; }

    public required IReadOnlyList<ManagementSystemTest> SystemTests { get; init; }

    public bool HasBlockers
        => Findings.Any(static f => f.Severity == ManagementPathAnalysisCodes.SeverityBlocker);

    /// <summary>True when any management-path BLOCKER is present (reconnect must fail closed).</summary>
    public bool BlocksManagementPath => HasBlockers;
}
