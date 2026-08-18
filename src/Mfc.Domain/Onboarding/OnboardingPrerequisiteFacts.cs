using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Observed IP service row for onboarding prerequisites (Onboarding Spec §9).</summary>
public sealed class OnboardingIpServiceFacts
{
    public required bool Found { get; init; }

    public required bool Disabled { get; init; }

    public ushort? Port { get; init; }

    public string? Certificate { get; init; }

    public string? AddressPrefixes { get; init; }

    public int? MaxSessions { get; init; }

    public static OnboardingIpServiceFacts Create(
        bool found,
        bool disabled,
        ushort? port = null,
        string? certificate = null,
        string? addressPrefixes = null,
        int? maxSessions = null)
        => new()
        {
            Found = found,
            Disabled = disabled,
            Port = port,
            Certificate = string.IsNullOrWhiteSpace(certificate) ? null : certificate.Trim(),
            AddressPrefixes = string.IsNullOrWhiteSpace(addressPrefixes) ? null : addressPrefixes.Trim(),
            MaxSessions = maxSessions,
        };
}

/// <summary>Local RouterOS service account observation (Onboarding Spec §10). No passwords.</summary>
public sealed class OnboardingServiceAccountFacts
{
    public required string Name { get; init; }

    public required string GroupName { get; init; }

    public required bool IsDefaultGroup { get; init; }

    public required IReadOnlyList<string> Policies { get; init; }

    public required IReadOnlyList<string> AddressPrefixes { get; init; }

    public static OnboardingServiceAccountFacts Create(
        string name,
        string groupName,
        bool isDefaultGroup,
        IEnumerable<string> policies,
        IEnumerable<string> addressPrefixes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(addressPrefixes);
        return new OnboardingServiceAccountFacts
        {
            Name = name.Trim(),
            GroupName = groupName.Trim(),
            IsDefaultGroup = isDefaultGroup,
            Policies = policies
                .Select(static p =>
                {
                    if (string.IsNullOrWhiteSpace(p))
                    {
                        throw new DomainInvariantException("Account policy names must be non-empty.");
                    }

                    return p.Trim().ToLowerInvariant();
                })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static p => p, StringComparer.Ordinal)
                .ToArray(),
            AddressPrefixes = addressPrefixes
                .Select(static a =>
                {
                    if (string.IsNullOrWhiteSpace(a))
                    {
                        throw new DomainInvariantException("Account address prefixes must be non-empty.");
                    }

                    return a.Trim();
                })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static a => a, StringComparer.Ordinal)
                .ToArray(),
        };
    }
}

/// <summary>Device-mode observation (Onboarding Spec §11). Read-only; never mutated by Controller.</summary>
public sealed class OnboardingDeviceModeFacts
{
    public required bool SchedulerEnabled { get; init; }

    public required bool Flagged { get; init; }

    public static OnboardingDeviceModeFacts Create(bool schedulerEnabled, bool flagged)
        => new() { SchedulerEnabled = schedulerEnabled, Flagged = flagged };
}

/// <summary>
/// Per-device prerequisite observation bundle (Onboarding Spec §7–§11 / M5-02).
/// Credentials and write probes are out of scope; facts are read-only inputs.
/// </summary>
public sealed record OnboardingDevicePrerequisiteFacts
{
    public required DeviceId DeviceId { get; init; }

    public required CapabilityProfile Capability { get; init; }

    public required bool ExactSupportedBuild { get; init; }

    public required OnboardingIpServiceFacts PlainApi { get; init; }

    public required OnboardingIpServiceFacts ApiSsl { get; init; }

    public required OnboardingServiceAccountFacts ReadAccount { get; init; }

    public required OnboardingServiceAccountFacts DeploymentAccount { get; init; }

    public required OnboardingDeviceModeFacts DeviceMode { get; init; }

    public required ushort ExpectedApiSslPort { get; init; }

    public static OnboardingDevicePrerequisiteFacts Create(
        DeviceId deviceId,
        CapabilityProfile capability,
        bool exactSupportedBuild,
        OnboardingIpServiceFacts plainApi,
        OnboardingIpServiceFacts apiSsl,
        OnboardingServiceAccountFacts readAccount,
        OnboardingServiceAccountFacts deploymentAccount,
        OnboardingDeviceModeFacts deviceMode,
        ushort expectedApiSslPort = ManagementEndpoint.DefaultApiSslPort)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(plainApi);
        ArgumentNullException.ThrowIfNull(apiSsl);
        ArgumentNullException.ThrowIfNull(readAccount);
        ArgumentNullException.ThrowIfNull(deploymentAccount);
        ArgumentNullException.ThrowIfNull(deviceMode);
        if (expectedApiSslPort == 0)
        {
            throw new DomainInvariantException("Expected API-SSL port must be non-zero.");
        }

        return new OnboardingDevicePrerequisiteFacts
        {
            DeviceId = deviceId,
            Capability = capability,
            ExactSupportedBuild = exactSupportedBuild,
            PlainApi = plainApi,
            ApiSsl = apiSsl,
            ReadAccount = readAccount,
            DeploymentAccount = deploymentAccount,
            DeviceMode = deviceMode,
            ExpectedApiSslPort = expectedApiSslPort,
        };
    }
}
