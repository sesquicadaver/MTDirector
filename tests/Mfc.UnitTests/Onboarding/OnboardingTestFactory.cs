using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.UnitTests.Onboarding;

internal static class OnboardingTestFactory
{
    public static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static Node RouterWithDevice(out Device device)
    {
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("r1"), NodeKind.Router, DeclaredUplinkMode.One);
        device = node.AddDevice(NonEmptyName.Create("r1-dev"), ManagementEndpoint.Create("10.0.0.1"), DeviceRole.Router);
        return node;
    }

    public static Node RouterWithUplink(DeclaredUplinkMode mode, out Device device)
    {
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("mw1"), NodeKind.Router, mode);
        device = node.AddDevice(NonEmptyName.Create("mw1-dev"), ManagementEndpoint.Create("10.0.0.8"), DeviceRole.Router);
        return node;
    }

    public static Node SwitchWithDevice(out Device device)
    {
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("sw1"), NodeKind.Switch, DeclaredUplinkMode.None);
        device = node.AddDevice(NonEmptyName.Create("sw1-dev"), ManagementEndpoint.Create("10.0.0.5"), DeviceRole.L2Switch);
        return node;
    }

    public static Node VrrpWithMembers(out Device first, out Device second)
    {
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("vrrp1"), NodeKind.Vrrp, DeclaredUplinkMode.Failover);
        first = node.AddDevice(NonEmptyName.Create("m1"), ManagementEndpoint.Create("10.0.1.1"), DeviceRole.Router);
        second = node.AddDevice(NonEmptyName.Create("m2"), ManagementEndpoint.Create("10.0.1.2"), DeviceRole.Router);
        return node;
    }

    public static DeviceOnboardingPlan DevicePlan(
        DeviceId deviceId,
        NodeKind kind,
        bool ipv6 = false,
        Hash256? configurationHash = null,
        TimeSpan? watchdogTtl = null,
        string version = "7.16.2")
    {
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(kind, ipv6);
        List<AnchorPlacement> placements = [];
        uint ordinal = 0;
        foreach (AnchorKey key in keys)
        {
            placements.Add(AnchorPlacement.Create(
                key.Family,
                key.Chain,
                AnchorPlacementMode.Append,
                expectedAnchorOrdinal: ordinal));
            ordinal++;
        }

        return DeviceOnboardingPlan.Create(
            deviceId,
            version,
            H("cap"),
            configurationHash ?? H("cfg"),
            H("compat"),
            H("api"),
            H("read"),
            H("deploy"),
            H("mode"),
            H("guard"),
            keys,
            placements,
            BootstrapArtifact.Hash,
            watchdogTtl);
    }

    public static OnboardingPlan PlanFor(
        Node node,
        DateTimeOffset? created = null,
        DateTimeOffset? expires = null,
        bool includeIpv6 = false)
    {
        DateTimeOffset now = created ?? new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        List<DeviceOnboardingPlan> devicePlans = [];
        foreach (Device device in node.Devices.OrderBy(static d => d.Id.Value))
        {
            devicePlans.Add(DevicePlan(device.Id, node.DeclaredKind, ipv6: includeIpv6));
        }

        return OnboardingPlan.Create(
            node,
            H("membership"),
            H("topology"),
            devicePlans,
            UserId.New(),
            now,
            expires);
    }
}
