using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Onboarding;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-02 AC 1–12 (Onboarding Spec §7–§11 / §58).
/// </summary>
public sealed class OnboardingPrerequisiteLivingSpecTests
{
    private static readonly Hash256 Manifest = H("manifest");

    [Fact]
    public void Ac1ExactSupportedBuildIsRequired()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts bad = ValidFacts(device.Id) with
        {
            ExactSupportedBuild = false,
            Capability = Capability(
                RouterOsVersion.Create(7, 16, 2, "stable"),
                SupportState.NeedsRevalidation),
        };
        OnboardingPrerequisiteResult result = OnboardingPrerequisiteValidator.Validate(
            node,
            new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = bad });
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.RouterOsUnsupported);
        Assert.True(result.HasBlockers);
    }

    [Fact]
    public void Ac2PlainApi8728MustBeDisabled()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            PlainApi = OnboardingIpServiceFacts.Create(found: true, disabled: false, port: 8728),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.PlainApiEnabled);
    }

    [Fact]
    public void Ac3ApiSslCertificateIsMandatory()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            ApiSsl = OnboardingIpServiceFacts.Create(
                found: true,
                disabled: false,
                port: 8729,
                certificate: "none",
                maxSessions: 2),
            Capability = Capability(
                RouterOsVersion.Create(7, 16, 2, "stable"),
                SupportState.Supported,
                apiSslCertificatePresent: false),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.ApiSslInvalid);
    }

    [Fact]
    public void Ac3ApiSslMaxSessionsMissingIsFailClosed()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            ApiSsl = OnboardingIpServiceFacts.Create(
                found: true,
                disabled: false,
                port: 8729,
                certificate: "mfc-api",
                maxSessions: null),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(
            result.Findings,
            static f => f.Code == OnboardingCodes.ApiSslInvalid && f.Target == "api-ssl.max-sessions");
    }

    [Fact]
    public void Ac4ReadAndDeploymentAccountsMustBeSeparated()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingServiceAccountFacts shared = ReadAccount("mfc-shared");
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            ReadAccount = shared,
            DeploymentAccount = DeployAccount("mfc-shared"),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.ReadAccountInvalid);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.DeployAccountInvalid);
    }

    [Fact]
    public void Ac5DefaultRouterOsGroupsAreRejected()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            ReadAccount = OnboardingServiceAccountFacts.Create(
                "mfc-read",
                "read",
                isDefaultGroup: true,
                policies: ["api", "read"],
                addressPrefixes: ["10.0.0.0/24"]),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.ReadAccountInvalid);
    }

    [Fact]
    public void Ac6RequiredAndForbiddenPoliciesAreChecked()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts missing = ValidFacts(device.Id) with
        {
            ReadAccount = OnboardingServiceAccountFacts.Create(
                "mfc-read",
                "mfc-read-group",
                isDefaultGroup: false,
                policies: ["api"],
                addressPrefixes: ["10.0.0.0/24"]),
        };
        Assert.Contains(
            Run(node, device.Id, missing).Findings,
            static f => f.Code == OnboardingCodes.ReadAccountInvalid && f.Target == "read");

        OnboardingDevicePrerequisiteFacts forbidden = ValidFacts(device.Id) with
        {
            DeploymentAccount = OnboardingServiceAccountFacts.Create(
                "mfc-deploy",
                "mfc-deploy-group",
                isDefaultGroup: false,
                policies: ["api", "read", "write", "test", "sensitive"],
                addressPrefixes: ["10.0.0.0/24"]),
        };
        Assert.Contains(
            Run(node, device.Id, forbidden).Findings,
            static f => f.Code == OnboardingCodes.DeployAccountInvalid && f.Target == "sensitive");
    }

    [Fact]
    public void Ac7SourceAddressRestrictionsAreChecked()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            ReadAccount = OnboardingServiceAccountFacts.Create(
                "mfc-read",
                "mfc-read-group",
                isDefaultGroup: false,
                policies: ["api", "read"],
                addressPrefixes: []),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.AccountSourceInvalid);
    }

    [Fact]
    public void Ac8SchedulerYesIsRequired()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            DeviceMode = OnboardingDeviceModeFacts.Create(schedulerEnabled: false, flagged: false),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.DeviceModeSchedulerDisabled);
    }

    [Fact]
    public void Ac9FlaggedNoIsRequired()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingDevicePrerequisiteFacts facts = ValidFacts(device.Id) with
        {
            DeviceMode = OnboardingDeviceModeFacts.Create(schedulerEnabled: true, flagged: true),
        };
        OnboardingPrerequisiteResult result = Run(node, device.Id, facts);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.DeviceFlagged);
    }

    [Fact]
    public void Ac10ControllerDoesNotExposeMutatorsForUsersServicesOrDeviceMode()
    {
        Assert.Null(typeof(OnboardingPrerequisiteValidator).GetMethod("Apply"));
        Assert.Null(typeof(OnboardingPrerequisiteValidator).GetMethod("EnableScheduler"));
        Assert.Null(typeof(OnboardingPrerequisiteValidator).GetMethod("DisablePlainApi"));
        Assert.Null(typeof(OnboardingPrerequisiteValidator).GetMethod("CreateUser"));
        Assert.Null(typeof(OnboardingDeviceModeFacts).GetMethod("SetScheduler"));
        Assert.Null(typeof(OnboardingIpServiceFacts).GetMethod("Disable"));
        Assert.DoesNotContain(
            typeof(OnboardingPrerequisiteValidator).GetMethods(),
            static m => m.DeclaringType == typeof(OnboardingPrerequisiteValidator)
                        && m.Name.StartsWith("Set", StringComparison.Ordinal));
        Type? writeNs = typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes()
            .FirstOrDefault(static t => t.Namespace == "Mfc.RouterOs.Write");
        Assert.Null(writeNs);
    }

    [Fact]
    public void Ac11AllVrrpMembersMustPassPrerequisites()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> map = new()
        {
            [first.Id] = ValidFacts(first.Id),
            [second.Id] = ValidFacts(second.Id) with
            {
                DeviceMode = OnboardingDeviceModeFacts.Create(schedulerEnabled: false, flagged: false),
            },
        };
        OnboardingPrerequisiteResult result = OnboardingPrerequisiteValidator.Validate(node, map);
        Assert.Contains(
            result.Findings,
            f => f.Code == OnboardingCodes.DeviceModeSchedulerDisabled && f.DeviceId == second.Id);

        map[second.Id] = ValidFacts(second.Id) with
        {
            Capability = Capability(RouterOsVersion.Create(7, 15, 3, "stable"), SupportState.Supported),
        };
        OnboardingPrerequisiteResult mismatch = OnboardingPrerequisiteValidator.Validate(node, map);
        Assert.Contains(mismatch.Findings, static f => f.Code == OnboardingCodes.RouterOsUnsupported);
    }

    [Fact]
    public void Ac12FindingsUseStableSpec58Codes()
    {
        string[] required =
        [
            OnboardingCodes.RouterOsUnsupported,
            OnboardingCodes.ApiSslInvalid,
            OnboardingCodes.PlainApiEnabled,
            OnboardingCodes.ReadAccountInvalid,
            OnboardingCodes.DeployAccountInvalid,
            OnboardingCodes.AccountSourceInvalid,
            OnboardingCodes.DeviceModeSchedulerDisabled,
            OnboardingCodes.DeviceFlagged,
        ];
        foreach (string code in required)
        {
            Assert.False(string.IsNullOrWhiteSpace(code));
            Assert.DoesNotContain(' ', code);
        }

        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPrerequisiteResult ok = Run(node, device.Id, ValidFacts(device.Id));
        Assert.True(ok.Passed);
        Assert.Empty(ok.Findings);
        Assert.Equal("mfc.onboarding.prerequisites.v1", OnboardingPrerequisiteValidator.AnalyzerVersion);
    }

    [Fact]
    public void ApplicationUseCaseDelegatesWithoutMutation()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        OnboardingPrerequisiteResult result = ValidateOnboardingPrerequisitesUseCase.Execute(
            node,
            new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = ValidFacts(device.Id) });
        Assert.True(result.Passed);
    }

    private static OnboardingPrerequisiteResult Run(
        Node node,
        DeviceId deviceId,
        OnboardingDevicePrerequisiteFacts facts)
        => OnboardingPrerequisiteValidator.Validate(
            node,
            new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [deviceId] = facts });

    private static OnboardingDevicePrerequisiteFacts ValidFacts(DeviceId deviceId)
        => OnboardingDevicePrerequisiteFacts.Create(
            deviceId,
            Capability(RouterOsVersion.Create(7, 16, 2, "stable"), SupportState.Supported),
            exactSupportedBuild: true,
            OnboardingIpServiceFacts.Create(found: true, disabled: true, port: 8728),
            OnboardingIpServiceFacts.Create(
                found: true,
                disabled: false,
                port: 8729,
                certificate: "mfc-api",
                maxSessions: 4),
            ReadAccount("mfc-read"),
            DeployAccount("mfc-deploy"),
            OnboardingDeviceModeFacts.Create(schedulerEnabled: true, flagged: false));

    private static OnboardingServiceAccountFacts ReadAccount(string name)
        => OnboardingServiceAccountFacts.Create(
            name,
            "mfc-read-group",
            isDefaultGroup: false,
            policies: ["api", "read"],
            addressPrefixes: ["10.0.0.0/24"]);

    private static OnboardingServiceAccountFacts DeployAccount(string name)
        => OnboardingServiceAccountFacts.Create(
            name,
            "mfc-deploy-group",
            isDefaultGroup: false,
            policies: ["api", "read", "write", "test"],
            addressPrefixes: ["10.0.0.0/24"]);

    private static CapabilityProfile Capability(
        RouterOsVersion version,
        SupportState support,
        bool apiSslCertificatePresent = true)
        => CapabilityProfile.Create(
            version,
            NonEmptyName.Create("x86_64"),
            NonEmptyName.Create("CHR"),
            packages: ["routeros", "ipv6"],
            ipv6Supported: true,
            vrrpSupported: true,
            bridgeSupported: true,
            apiSslCertificatePresent,
            support,
            Manifest);

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
