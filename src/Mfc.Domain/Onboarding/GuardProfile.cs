using System.Net;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Typed external management guard profile (Onboarding Spec §13–§14).
/// Controller never creates or mutates the live guard; this is verification-only identity.
/// </summary>
public sealed class GuardProfile
{
    private GuardProfile(
        GuardProfileId id,
        DeviceId deviceId,
        IpAddressFamily family,
        IReadOnlyList<AddressPrefix> controllerSourcePrefixes,
        IPAddress managementDestination,
        ushort apiSslPort,
        IReadOnlyList<string> ingressInterfaceSet,
        IReadOnlyList<string> inputRuleMarkers,
        IReadOnlyList<string> outputRuleMarkers,
        Hash256 canonicalHash)
    {
        Id = id;
        DeviceId = deviceId;
        Family = family;
        ControllerSourcePrefixes = controllerSourcePrefixes;
        ManagementDestination = managementDestination;
        ApiSslPort = apiSslPort;
        IngressInterfaceSet = ingressInterfaceSet;
        InputRuleMarkers = inputRuleMarkers;
        OutputRuleMarkers = outputRuleMarkers;
        CanonicalHash = canonicalHash;
    }

    public GuardProfileId Id { get; }

    public DeviceId DeviceId { get; }

    public IpAddressFamily Family { get; }

    public IReadOnlyList<AddressPrefix> ControllerSourcePrefixes { get; }

    public IPAddress ManagementDestination { get; }

    public ushort ApiSslPort { get; }

    public IReadOnlyList<string> IngressInterfaceSet { get; }

    public IReadOnlyList<string> InputRuleMarkers { get; }

    public IReadOnlyList<string> OutputRuleMarkers { get; }

    /// <summary>Content-address of this profile (<see cref="GuardProfileHasher"/>).</summary>
    public Hash256 CanonicalHash { get; }

    public static GuardProfile Create(
        GuardProfileId id,
        DeviceId deviceId,
        IpAddressFamily family,
        IReadOnlyList<AddressPrefix> controllerSourcePrefixes,
        IPAddress managementDestination,
        ushort apiSslPort,
        IReadOnlyList<string> inputRuleMarkers,
        IReadOnlyList<string> outputRuleMarkers,
        IReadOnlyList<string>? ingressInterfaceSet = null)
    {
        ArgumentNullException.ThrowIfNull(controllerSourcePrefixes);
        ArgumentNullException.ThrowIfNull(managementDestination);
        ArgumentNullException.ThrowIfNull(inputRuleMarkers);
        ArgumentNullException.ThrowIfNull(outputRuleMarkers);

        if (family is not (IpAddressFamily.IPv4 or IpAddressFamily.IPv6))
        {
            throw new DomainInvariantException($"Unsupported GuardProfile family '{family}'.");
        }

        if (apiSslPort == 0)
        {
            throw new DomainInvariantException("GuardProfile api_ssl_port must be non-zero.");
        }

        if (controllerSourcePrefixes.Count == 0)
        {
            throw new DomainInvariantException("GuardProfile requires at least one controller source prefix.");
        }

        if (inputRuleMarkers.Count == 0 || outputRuleMarkers.Count == 0)
        {
            throw new DomainInvariantException(
                "GuardProfile requires at least one input and one output guard marker.");
        }

        IpAddressFamily destFamily = managementDestination.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => IpAddressFamily.IPv4,
            System.Net.Sockets.AddressFamily.InterNetworkV6 => IpAddressFamily.IPv6,
            _ => throw new DomainInvariantException("Management destination must be IPv4 or IPv6."),
        };
        if (destFamily != family)
        {
            throw new DomainInvariantException("Management destination family must match GuardProfile.family.");
        }

        List<AddressPrefix> prefixes = new(controllerSourcePrefixes.Count);
        foreach (AddressPrefix prefix in controllerSourcePrefixes)
        {
            ArgumentNullException.ThrowIfNull(prefix);
            if (prefix.Family != family)
            {
                throw new DomainInvariantException("Controller source prefix family must match GuardProfile.family.");
            }

            if (IsDefaultRoute(prefix))
            {
                throw new DomainInvariantException(
                    $"{OnboardingCodes.ManagementGuardTooBroad}: GuardProfile rejects {prefix}.");
            }

            prefixes.Add(prefix);
        }

        List<string> inputs = NormalizeMarkers(inputRuleMarkers, id, family, FilterBuiltInContext.Input);
        List<string> outputs = NormalizeMarkers(outputRuleMarkers, id, family, FilterBuiltInContext.Output);
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string marker in inputs.Concat(outputs))
        {
            if (!unique.Add(marker))
            {
                throw new DomainInvariantException($"Duplicate guard marker '{marker}'.");
            }
        }

        List<string> ingress = NormalizeInterfaces(ingressInterfaceSet);
        Hash256 hash = GuardProfileHasher.Compute(
            id,
            deviceId,
            family,
            prefixes,
            managementDestination,
            apiSslPort,
            ingress,
            inputs,
            outputs);

        return new GuardProfile(
            id,
            deviceId,
            family,
            prefixes.OrderBy(static p => p.ToString(), StringComparer.Ordinal).ToArray(),
            managementDestination,
            apiSslPort,
            ingress,
            inputs,
            outputs,
            hash);
    }

    /// <summary>Maps this typed profile onto M2-13 <see cref="ManagementAccessProfile"/>.</summary>
    public ManagementAccessProfile ToManagementAccessProfile()
        => ManagementAccessProfile.Create(
            ControllerSourcePrefixes,
            ManagementDestination.ToString(),
            ApiSslPort,
            expectedIngressInterface: IngressInterfaceSet.Count == 1 ? IngressInterfaceSet[0] : null,
            physicalManagementAddresses: [ManagementDestination.ToString()]);

    internal static bool IsDefaultRoute(AddressPrefix prefix)
        => prefix.PrefixLength == 0;

    private static List<string> NormalizeMarkers(
        IReadOnlyList<string> markers,
        GuardProfileId id,
        IpAddressFamily family,
        FilterBuiltInContext chain)
    {
        List<string> normalized = new(markers.Count);
        foreach (string raw in markers)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new DomainInvariantException("Guard marker entries must be non-empty.");
            }

            string marker = raw.Trim();
            if (!GuardMarker.TryParse(marker, out GuardProfileId parsedId, out IpAddressFamily parsedFamily,
                    out FilterBuiltInContext parsedChain, out _)
                || parsedId.Value != id.Value
                || parsedFamily != family
                || parsedChain != chain)
            {
                throw new DomainInvariantException(
                    $"Guard marker '{marker}' must be strict mfc:guard:v1 for this profile/family/chain.");
            }

            // Marker string alone (no trailing display text) is stored on the profile.
            if (!string.Equals(marker, marker.Split(',', ';', ' ', '\t')[0], StringComparison.Ordinal)
                || marker.Contains(' ', StringComparison.Ordinal)
                || marker.Contains(',', StringComparison.Ordinal))
            {
                throw new DomainInvariantException("GuardProfile marker lists store the marker token only.");
            }

            normalized.Add(marker);
        }

        return normalized.OrderBy(static m => m, StringComparer.Ordinal).ToList();
    }

    private static List<string> NormalizeInterfaces(IReadOnlyList<string>? interfaces)
    {
        if (interfaces is null || interfaces.Count == 0)
        {
            return [];
        }

        List<string> normalized = new(interfaces.Count);
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string raw in interfaces)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new DomainInvariantException("Ingress interface set entries must be non-empty.");
            }

            string name = raw.Trim();
            if (!unique.Add(name))
            {
                throw new DomainInvariantException($"Duplicate ingress interface '{name}'.");
            }

            normalized.Add(name);
        }

        return normalized.OrderBy(static n => n, StringComparer.Ordinal).ToList();
    }
}
