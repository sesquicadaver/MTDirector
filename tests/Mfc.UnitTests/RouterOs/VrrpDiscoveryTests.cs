using System.Text.Json;
using Mfc.Domain.Inventory;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Discovery;
using Mfc.RouterOs.Redaction;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class VrrpDiscoveryTests
{
    [Fact]
    public void GroupsByFamilyVridInterfaceAndSupportsMixedRoles()
    {
        VrrpDiscoveryResult result = VrrpDiscovery.BuildResult(
            Ok(
                Row(
                    ("name", "vrrp-wan1"),
                    ("interface", "ether1"),
                    ("vrid", "10"),
                    ("priority", "200"),
                    ("v3-protocol", "ipv4"),
                    ("master", "true"),
                    ("backup", "false"),
                    ("running", "true")),
                Row(
                    ("name", "vrrp-wan2"),
                    ("interface", "ether2"),
                    ("vrid", "20"),
                    ("priority", "100"),
                    ("v3-protocol", "ipv4"),
                    ("master", "false"),
                    ("backup", "true"),
                    ("running", "true")),
                Row(
                    ("name", "vrrp-lan6"),
                    ("interface", "bridge1"),
                    ("vrid", "10"),
                    ("priority", "255"),
                    ("v3-protocol", "ipv6"),
                    ("master", "true"),
                    ("backup", "false"),
                    ("running", "true"))),
            addressBindings: AddressesFor(
                ("vrrp-wan1", "10.255.50.20/32", IpAddressFamilyKind.Ipv4),
                ("vrrp-wan2", "10.255.60.20/32", IpAddressFamilyKind.Ipv4),
                ("vrrp-lan6", "2001:db8:50::1/128", IpAddressFamilyKind.Ipv6)));

        Assert.Equal(3, result.Instances.Count);
        Assert.True(result.HasMixedMasterAndBackupRoles);
        Assert.Contains(
            result.Instances,
            i => i.GroupKey.Equals(new VrrpGroupKey(IpAddressFamilyKind.Ipv4, 10, "ether1"))
                 && i.ObservedRole == VrrpDerivedRole.Master
                 && i.VirtualAddresses.Contains("10.255.50.20/32"));
        Assert.Contains(
            result.Instances,
            i => i.GroupKey.Equals(new VrrpGroupKey(IpAddressFamilyKind.Ipv6, 10, "bridge1"))
                 && i.IsOwner
                 && i.Priority == 255);
        // Same VRID number with different families remains distinct.
        Assert.Equal(2, result.Instances.Count(i => i.Vrid == 10));
    }

    [Fact]
    public void RoleChangeAffectsObservationHashNotConfigurationHash()
    {
        RosReadCommandResult config = Ok(
            Row(
                ("name", "vrrp1"),
                ("interface", "ether1"),
                ("vrid", "1"),
                ("priority", "150"),
                ("version", "3"),
                ("preemption-mode", "yes"),
                ("authentication", "none"),
                ("disabled", "false"),
                ("master", "true"),
                ("backup", "false"),
                ("running", "true")));

        VrrpDiscoveryResult master = VrrpDiscovery.BuildResult(config);
        Dictionary<string, string> masterKnown = config.Records[0].KnownProperties.ToDictionary(
            kv => kv.Key,
            kv => kv.Value,
            StringComparer.Ordinal);
        masterKnown["master"] = "false";
        masterKnown["backup"] = "true";
        RosReadCommandResult backupRow = Ok(
            new RosReadRecord
            {
                KnownProperties = masterKnown,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            });
        VrrpDiscoveryResult backup = VrrpDiscovery.BuildResult(backupRow);

        Assert.Equal(master.ConfigurationHashMaterial, backup.ConfigurationHashMaterial);
        Assert.NotEqual(master.ObservationHashMaterial, backup.ObservationHashMaterial);
        Assert.Equal(VrrpDerivedRole.Master, master.Instances[0].ObservedRole);
        Assert.Equal(VrrpDerivedRole.Backup, backup.Instances[0].ObservedRole);
    }

    [Theory]
    [InlineData("true", "false", "false", "false", "false", VrrpDerivedRole.Failure)]
    [InlineData("false", "false", "false", "true", "false", VrrpDerivedRole.Invalid)]
    [InlineData("false", "false", "false", "false", "true", VrrpDerivedRole.Initializing)]
    [InlineData("false", "false", "false", "false", "false", VrrpDerivedRole.Inactive)]
    public void SupportsInitInvalidFailureAndUnknownMappedStates(
        string failure,
        string master,
        string backup,
        string invalid,
        string running,
        VrrpDerivedRole expected)
    {
        VrrpDerivedRole role = VrrpDiscovery.DeriveRole(failure, master, backup, invalid, running, out _);
        Assert.Equal(expected, role);
        Assert.Equal(
            expected is VrrpDerivedRole.Initializing ? VrrpMemberObservedState.Init : VrrpMemberObservedState.Unknown,
            VrrpDiscovery.BuildResult(
                Ok(Row(
                    ("name", "vrrp1"),
                    ("interface", "ether1"),
                    ("vrid", "1"),
                    ("priority", "100"),
                    ("failure", failure),
                    ("master", master),
                    ("backup", backup),
                    ("invalid", invalid),
                    ("running", running)))).Instances[0].DomainObservedState);
    }

    [Fact]
    public void InconsistentMasterAndBackupProducesFinding()
    {
        VrrpDiscoveryResult result = VrrpDiscovery.BuildResult(
            Ok(Row(
                ("name", "vrrp1"),
                ("interface", "ether1"),
                ("vrid", "1"),
                ("priority", "100"),
                ("master", "true"),
                ("backup", "true"),
                ("running", "true"))));

        Assert.Equal(VrrpDerivedRole.Inconsistent, result.Instances[0].ObservedRole);
        Assert.Contains(result.Findings, f => f.Code == DiscoveryFinding.VrrpRoleInconsistent);
    }

    [Fact]
    public void ProfileNeverRequestsPasswordOrTransitionScripts()
    {
        string[] props = RosReadCommandRegistry.Get(RosReadCommandId.VrrpInterfaces)
            .PropertyProfile.ProplistValue.Split(',');
        Assert.DoesNotContain("password", props, StringComparer.Ordinal);
        Assert.DoesNotContain("on-master", props, StringComparer.Ordinal);
        Assert.DoesNotContain("on-backup", props, StringComparer.Ordinal);
        Assert.DoesNotContain("on-fail", props, StringComparer.Ordinal);
        Assert.True(SensitiveFieldRegistry.IsForbidden("password"));
    }

    [Fact]
    public void SanitizedFixtureCoversMultipleVridsAndSplitMaster()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "vrrp-discovery.sanitized.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement instances = doc.RootElement.GetProperty("instances");
        Assert.Equal(3, instances.GetArrayLength());
        Assert.Contains(
            instances.EnumerateArray(),
            i => i.GetProperty("observedRole").GetString() == "Master"
                 && i.GetProperty("vrid").GetInt32() == 10
                 && i.GetProperty("family").GetString() == "Ipv4");
        Assert.Contains(
            instances.EnumerateArray(),
            i => i.GetProperty("observedRole").GetString() == "Backup"
                 && i.GetProperty("vrid").GetInt32() == 20);
        Assert.Contains(
            instances.EnumerateArray(),
            i => i.GetProperty("family").GetString() == "Ipv6" && i.GetProperty("isOwner").GetBoolean());
        string json = doc.RootElement.ToString();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("on-master", json, StringComparison.OrdinalIgnoreCase);
    }

    private static InterfaceAddressDiscoveryResult AddressesFor(
        params (string Iface, string Cidr, IpAddressFamilyKind Family)[] rows)
    {
        List<IpAddressDiscovery> v4 = [];
        List<IpAddressDiscovery> v6 = [];
        foreach ((string iface, string cidr, IpAddressFamilyKind family) in rows)
        {
            IpAddressDiscovery item = new()
            {
                Family = family,
                Id = null,
                AddressCidr = cidr,
                AddressCidrRaw = cidr,
                AddressNormalized = true,
                Network = null,
                Interface = iface,
                Disabled = "false",
                Comment = null,
                FromPool = null,
                IsDynamic = false,
                ActualInterface = iface,
                RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
            };
            if (family == IpAddressFamilyKind.Ipv4)
            {
                v4.Add(item);
            }
            else
            {
                v6.Add(item);
            }
        }

        return new InterfaceAddressDiscoveryResult
        {
            Interfaces = [],
            Ipv4StaticAddresses = v4,
            Ipv4DynamicAddresses = [],
            Ipv6StaticAddresses = v6,
            Ipv6DynamicAddresses = [],
            InterfaceLists = [],
            InterfaceListMembers = [],
            ResolvedMembership = [],
            Findings = [],
            Warnings = [],
        };
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static RosReadCommandResult Ok(params RosReadRecord[] rows)
        => new()
        {
            CommandId = RosReadCommandId.VrrpInterfaces,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = rows,
            SessionInvalidated = false,
            Error = null,
        };

    private static RosReadRecord Row(params (string Name, string Value)[] properties)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        foreach ((string name, string value) in properties)
        {
            known[name] = value;
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
