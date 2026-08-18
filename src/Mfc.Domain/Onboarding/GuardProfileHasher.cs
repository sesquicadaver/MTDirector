using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Content-addresses <see cref="GuardProfile"/> for DeviceOnboardingPlan.ExpectedGuardHash
/// (Onboarding Spec §14 / §25 / M5-03 AC#9).
/// </summary>
public static class GuardProfileHasher
{
    public const string Prefix = "mfc.onboarding.guard.v1";

    public static Hash256 Compute(GuardProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Compute(
            profile.Id,
            profile.DeviceId,
            profile.Family,
            profile.ControllerSourcePrefixes,
            profile.ManagementDestination,
            profile.ApiSslPort,
            profile.IngressInterfaceSet,
            profile.InputRuleMarkers,
            profile.OutputRuleMarkers);
    }

    public static Hash256 Compute(
        GuardProfileId id,
        DeviceId deviceId,
        IpAddressFamily family,
        IReadOnlyList<AddressPrefix> controllerSourcePrefixes,
        IPAddress managementDestination,
        ushort apiSslPort,
        IReadOnlyList<string> ingressInterfaceSet,
        IReadOnlyList<string> inputRuleMarkers,
        IReadOnlyList<string> outputRuleMarkers)
    {
        ArgumentNullException.ThrowIfNull(controllerSourcePrefixes);
        ArgumentNullException.ThrowIfNull(managementDestination);
        ArgumentNullException.ThrowIfNull(ingressInterfaceSet);
        ArgumentNullException.ThrowIfNull(inputRuleMarkers);
        ArgumentNullException.ThrowIfNull(outputRuleMarkers);

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, Prefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, id.Value);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, deviceId.Value.ToString("D"));
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)family]);
        AppendUInt16Be(hasher, apiSslPort);
        AppendUtf8(hasher, managementDestination.ToString());
        hasher.AppendData([(byte)0]);
        foreach (AddressPrefix prefix in controllerSourcePrefixes.OrderBy(static p => p.ToString(), StringComparer.Ordinal))
        {
            AppendUtf8(hasher, prefix.ToString());
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string iface in ingressInterfaceSet.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, iface);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string marker in inputRuleMarkers.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, marker);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string marker in outputRuleMarkers.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, marker);
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));

    private static void AppendUInt16Be(IncrementalHash hasher, ushort value)
    {
        Span<byte> slot = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(slot, value);
        hasher.AppendData(slot);
    }
}
