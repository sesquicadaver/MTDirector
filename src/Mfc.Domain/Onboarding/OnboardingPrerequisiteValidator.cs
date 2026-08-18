using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Pure RouterOS prerequisite validation (Onboarding Spec §7–§11 / Issue Set M5-02).
/// Does not mutate users, IP services, certificates, or device-mode.
/// </summary>
public static class OnboardingPrerequisiteValidator
{
    public const string AnalyzerVersion = "mfc.onboarding.prerequisites.v1";

    private static readonly HashSet<string> DefaultGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "read",
        "write",
        "full",
        "reboot",
    };

    private static readonly string[] ReadRequired = ["api", "read"];

    private static readonly string[] ReadForbidden =
    [
        "local", "telnet", "ssh", "ftp", "winbox", "web", "rest-api", "romon",
        "write", "policy", "test", "reboot", "password", "sniff", "sensitive",
    ];

    private static readonly string[] DeployRequired = ["api", "read", "write", "test"];

    private static readonly string[] DeployForbidden =
    [
        "local", "telnet", "ssh", "ftp", "winbox", "web", "rest-api", "romon",
        "reboot", "policy", "password", "sniff", "sensitive",
    ];

    /// <summary>
    /// Validates every enabled Device on the Node. VRRP requires facts for each member (AC#11).
    /// </summary>
    public static OnboardingPrerequisiteResult Validate(
        Node node,
        IReadOnlyDictionary<DeviceId, OnboardingDevicePrerequisiteFacts> factsByDevice)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(factsByDevice);
        List<OnboardingPrerequisiteFinding> findings = [];
        Device[] enabled = [.. node.Devices.Where(static d => d.Enabled).OrderBy(static d => d.Id.Value)];
        if (enabled.Length == 0)
        {
            findings.Add(Blocker(
                OnboardingCodes.DevicePlanCardinality,
                "Node has no enabled Devices for prerequisite validation.",
                deviceId: null));
            return Finish(findings);
        }

        List<OnboardingDevicePrerequisiteFacts> orderedFacts = [];
        foreach (Device device in enabled)
        {
            if (!factsByDevice.TryGetValue(device.Id, out OnboardingDevicePrerequisiteFacts? facts)
                || facts is null)
            {
                findings.Add(Blocker(
                    OnboardingCodes.RouterOsUnsupported,
                    $"Missing prerequisite facts for device '{device.Id}'.",
                    device.Id));
                continue;
            }

            if (facts.DeviceId != device.Id)
            {
                findings.Add(Blocker(
                    OnboardingCodes.RouterOsUnsupported,
                    "Prerequisite facts device_id does not match the Node member.",
                    device.Id));
                continue;
            }

            orderedFacts.Add(facts);
            ValidateDevice(facts, findings);
        }

        if (node.DeclaredKind == NodeKind.Vrrp && orderedFacts.Count >= 2)
        {
            ValidateVrrpHomogeneousBuilds(orderedFacts, findings);
        }

        return Finish(findings);
    }

    private static void ValidateDevice(
        OnboardingDevicePrerequisiteFacts facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        ValidateBuild(facts, findings);
        ValidatePlainApi(facts, findings);
        ValidateApiSsl(facts, findings);
        ValidateAccounts(facts, findings);
        ValidateDeviceMode(facts, findings);
    }

    private static void ValidateBuild(
        OnboardingDevicePrerequisiteFacts facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        CapabilityProfile capability = facts.Capability;
        if (capability.Version.Major != 7
            || !facts.ExactSupportedBuild
            || capability.SupportState != SupportState.Supported)
        {
            findings.Add(Blocker(
                OnboardingCodes.RouterOsUnsupported,
                $"RouterOS build '{capability.Version}' is not an exact supported production build.",
                facts.DeviceId,
                capability.Version.ToString()));
        }
    }

    private static void ValidatePlainApi(
        OnboardingDevicePrerequisiteFacts facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        OnboardingIpServiceFacts plain = facts.PlainApi;
        if (!plain.Found)
        {
            findings.Add(Blocker(
                OnboardingCodes.PlainApiEnabled,
                "Plain API service was not discovered; cannot prove port 8728 is disabled.",
                facts.DeviceId,
                "api"));
            return;
        }

        bool looksLikePlain = plain.Port is null or 8728;
        if (looksLikePlain && !plain.Disabled)
        {
            findings.Add(Blocker(
                OnboardingCodes.PlainApiEnabled,
                "Plain API (8728) must be disabled.",
                facts.DeviceId,
                "api"));
        }
    }

    private static void ValidateApiSsl(
        OnboardingDevicePrerequisiteFacts facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        OnboardingIpServiceFacts apiSsl = facts.ApiSsl;
        CapabilityProfile capability = facts.Capability;
        if (!apiSsl.Found || apiSsl.Disabled)
        {
            findings.Add(Blocker(
                OnboardingCodes.ApiSslInvalid,
                "API-SSL service must be present and enabled.",
                facts.DeviceId,
                "api-ssl"));
            return;
        }

        if (string.IsNullOrWhiteSpace(apiSsl.Certificate)
            || string.Equals(apiSsl.Certificate, "none", StringComparison.OrdinalIgnoreCase)
            || !capability.ApiSslCertificatePresent)
        {
            findings.Add(Blocker(
                OnboardingCodes.ApiSslInvalid,
                "API-SSL certificate is mandatory (anonymous Diffie–Hellman is forbidden).",
                facts.DeviceId,
                "api-ssl.certificate"));
        }

        if (apiSsl.Port is null || apiSsl.Port.Value != facts.ExpectedApiSslPort)
        {
            findings.Add(Blocker(
                OnboardingCodes.ApiSslInvalid,
                $"API-SSL port must equal expected {facts.ExpectedApiSslPort}.",
                facts.DeviceId,
                "api-ssl.port"));
        }

        if (apiSsl.MaxSessions is null or < 2)
        {
            findings.Add(Blocker(
                OnboardingCodes.ApiSslInvalid,
                "API-SSL max-sessions must be >= 2.",
                facts.DeviceId,
                "api-ssl.max-sessions"));
        }
    }

    private static void ValidateAccounts(
        OnboardingDevicePrerequisiteFacts facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        OnboardingServiceAccountFacts read = facts.ReadAccount;
        OnboardingServiceAccountFacts deploy = facts.DeploymentAccount;
        if (string.Equals(read.Name, deploy.Name, StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Blocker(
                OnboardingCodes.ReadAccountInvalid,
                "Read and deployment accounts must be separate local users.",
                facts.DeviceId,
                read.Name));
            findings.Add(Blocker(
                OnboardingCodes.DeployAccountInvalid,
                "Read and deployment accounts must be separate local users.",
                facts.DeviceId,
                deploy.Name));
        }

        ValidateAccountRole(
            facts.DeviceId,
            read,
            OnboardingCodes.ReadAccountInvalid,
            ReadRequired,
            ReadForbidden,
            findings);
        ValidateAccountRole(
            facts.DeviceId,
            deploy,
            OnboardingCodes.DeployAccountInvalid,
            DeployRequired,
            DeployForbidden,
            findings);
    }

    private static void ValidateAccountRole(
        DeviceId deviceId,
        OnboardingServiceAccountFacts account,
        string invalidCode,
        IReadOnlyList<string> required,
        IReadOnlyList<string> forbidden,
        List<OnboardingPrerequisiteFinding> findings)
    {
        if (account.IsDefaultGroup || DefaultGroups.Contains(account.GroupName))
        {
            findings.Add(Blocker(
                invalidCode,
                $"Default RouterOS group '{account.GroupName}' is rejected; custom group required.",
                deviceId,
                account.GroupName));
        }

        foreach (string policy in required)
        {
            if (!account.Policies.Contains(policy, StringComparer.Ordinal))
            {
                findings.Add(Blocker(
                    invalidCode,
                    $"Account '{account.Name}' is missing required policy '{policy}'.",
                    deviceId,
                    policy));
            }
        }

        foreach (string policy in forbidden)
        {
            if (account.Policies.Contains(policy, StringComparer.Ordinal))
            {
                findings.Add(Blocker(
                    invalidCode,
                    $"Account '{account.Name}' has forbidden policy '{policy}'.",
                    deviceId,
                    policy));
            }
        }

        if (account.AddressPrefixes.Count == 0)
        {
            findings.Add(Blocker(
                OnboardingCodes.AccountSourceInvalid,
                $"Account '{account.Name}' must restrict login to controller source prefixes.",
                deviceId,
                account.Name));
        }
    }

    private static void ValidateDeviceMode(
        OnboardingDevicePrerequisiteFacts facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        if (!facts.DeviceMode.SchedulerEnabled)
        {
            findings.Add(Blocker(
                OnboardingCodes.DeviceModeSchedulerDisabled,
                "device-mode.scheduler=yes is required; Controller does not change device-mode.",
                facts.DeviceId,
                "scheduler"));
        }

        if (facts.DeviceMode.Flagged)
        {
            findings.Add(Blocker(
                OnboardingCodes.DeviceFlagged,
                "device-mode.flagged=no is required; Controller does not clear the flag.",
                facts.DeviceId,
                "flagged"));
        }
    }

    private static void ValidateVrrpHomogeneousBuilds(
        List<OnboardingDevicePrerequisiteFacts> facts,
        List<OnboardingPrerequisiteFinding> findings)
    {
        string first = facts[0].Capability.Version.ToString();
        foreach (OnboardingDevicePrerequisiteFacts member in facts.Skip(1))
        {
            if (!string.Equals(first, member.Capability.Version.ToString(), StringComparison.Ordinal))
            {
                findings.Add(Blocker(
                    OnboardingCodes.RouterOsUnsupported,
                    "VRRP members must share the same exact RouterOS build for onboarding.",
                    member.DeviceId,
                    member.Capability.Version.ToString()));
            }
        }
    }

    private static OnboardingPrerequisiteResult Finish(List<OnboardingPrerequisiteFinding> findings)
    {
        IReadOnlyList<OnboardingPrerequisiteFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.DeviceId, f.Target, f.Message))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.DeviceId?.Value ?? Guid.Empty)
            .ThenBy(static f => f.Target ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();
        return new OnboardingPrerequisiteResult { Findings = ordered };
    }

    private static OnboardingPrerequisiteFinding Blocker(
        string code,
        string message,
        DeviceId? deviceId,
        string? target = null)
        => new()
        {
            Code = code,
            Severity = OnboardingCodes.SeverityBlocker,
            Message = message,
            DeviceId = deviceId,
            Target = target,
        };
}
