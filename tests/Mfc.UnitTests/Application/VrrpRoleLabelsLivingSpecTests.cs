using Mfc.Application.Mapping;
using Mfc.Domain.Canonicalization;
using Xunit;

namespace Mfc.UnitTests.Application;

/// <summary>W2.3: GetNode VRRP labels come from last-capture ha.vrrp observations, never invented.</summary>
public sealed class VrrpRoleLabelsLivingSpecTests
{
    [Fact]
    public void Ac1EmptyObservationsStayEmpty()
    {
        Assert.Empty(DeviceVrrpRoleLabelProjector.FromCanonicalSections([]));
        CanonicalSection configOnly = new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["group"] = "Ipv4/vrid=10/if=ether1",
                    ["vrid"] = "10",
                }),
            ]);
        Assert.Empty(DeviceVrrpRoleLabelProjector.FromCanonicalSections([configOnly]));
    }

    [Fact]
    public void Ac2ObservationRolesBecomeLabelsWithoutPlaceholders()
    {
        CanonicalSection observations = new(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["group"] = "Ipv4/vrid=10/if=ether1",
                    ["role"] = "Master",
                }),
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["role"] = "   ",
                    ["group"] = "ignored",
                }),
            ]);

        IReadOnlyList<string> labels = DeviceVrrpRoleLabelProjector.FromCanonicalSections([observations]);
        Assert.Equal(["Master · Ipv4/vrid=10/if=ether1"], labels);
    }

    [Fact]
    public void Ac3DoesNotInventMasterOrBackupWhenRoleMissing()
    {
        CanonicalSection observations = new(
            CanonicalDomain.Observations,
            CanonicalSectionIds.HaVrrp,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["group"] = "Ipv4/vrid=10/if=ether1",
                    ["running"] = "true",
                }),
            ]);

        Assert.Empty(DeviceVrrpRoleLabelProjector.FromCanonicalSections([observations]));
        Assert.DoesNotContain(
            DeviceVrrpRoleLabelProjector.FromCanonicalSections([observations]),
            static l => l.Contains("Master", StringComparison.OrdinalIgnoreCase)
                || l.Contains("Backup", StringComparison.OrdinalIgnoreCase));
    }
}
