using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.UnitTests.Deployment;

internal static class DeploymentTestFactory
{
    public static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static Node RouterWithDevice(out Device device)
    {
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("r1"), NodeKind.Router, DeclaredUplinkMode.One);
        device = node.AddDevice(NonEmptyName.Create("r1-dev"), ManagementEndpoint.Create("10.0.0.1"), DeviceRole.Router);
        return node;
    }

    public static Node VrrpWithMembers(out Device first, out Device second)
    {
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("vrrp1"), NodeKind.Vrrp, DeclaredUplinkMode.Failover);
        first = node.AddDevice(NonEmptyName.Create("m1"), ManagementEndpoint.Create("10.0.1.1"), DeviceRole.Router);
        second = node.AddDevice(NonEmptyName.Create("m2"), ManagementEndpoint.Create("10.0.1.2"), DeviceRole.Router);
        return node;
    }

    public static DeviceDeploymentPlan DevicePlan(
        DeviceId deviceId,
        NodeKind kind,
        bool ipv6 = false,
        bool noChanges = false)
    {
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(kind, ipv6);
        List<AnchorTarget> oldTargets = [];
        List<AnchorTarget> newTargets = [];
        foreach (AnchorKey key in keys)
        {
            oldTargets.Add(new AnchorTarget(key, BootstrapArtifact.RootChainName(key.Family, key.Chain)));
            newTargets.Add(new AnchorTarget(
                key,
                noChanges
                    ? BootstrapArtifact.RootChainName(key.Family, key.Chain)
                    : $"mfc{(key.Family == IpAddressFamily.IPv4 ? "4" : "6")}.{AnchorKey.ChainCode(key.Chain)}.r.0123456789abcdef"));
        }

        Hash256 oldArt = H("old-art");
        Hash256 newArt = noChanges ? oldArt : H("new-art");
        return DeviceDeploymentPlan.Create(
            deviceId,
            "7.16.2",
            H("cap"),
            H("cfg"),
            H("compat"),
            H("guard-ctx"),
            H("anchor-ctx"),
            oldArt,
            oldTargets,
            newArt,
            newTargets,
            keys,
            keys.Reverse().ToArray(),
            [H("t0"), H("t1")],
            DeploymentCodes.DefaultRollbackTtl,
            [new DeploymentProbe(DeploymentProbeKind.IcmpEcho, "192.0.2.1", 500)]);
    }

    public static DeploymentPlan PlanFor(Node node, DateTimeOffset? created = null, bool noChanges = false)
    {
        DateTimeOffset now = created ?? new DateTimeOffset(2026, 8, 19, 21, 0, 0, TimeSpan.Zero);
        List<DeviceDeploymentPlan> devicePlans = [];
        foreach (Device device in node.Devices.OrderBy(static d => d.Id.Value))
        {
            devicePlans.Add(DevicePlan(device.Id, node.DeclaredKind, noChanges: noChanges));
        }

        return DeploymentPlan.Create(node, H("policy"), H("analysis"), H("topology"), devicePlans, UserId.New(), now);
    }
}
