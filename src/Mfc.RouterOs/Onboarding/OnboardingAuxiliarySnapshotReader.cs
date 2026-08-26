using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Session;
using Mfc.RouterOs.Snapshot;

namespace Mfc.RouterOs.Onboarding;

/// <summary>
/// Reads non-filter auxiliary hashes for onboarding verification (Onboarding Spec §40.14–§40.17 / P2-07).
/// </summary>
internal static class OnboardingAuxiliarySnapshotReader
{
    public static async Task<OnboardingAuxiliarySnapshot> ReadAsync(
        RosSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new OnboardingAuxiliarySnapshot
        {
            NatHash = await CombineAsync(
                session,
                [RosReadCommandId.Ipv4Nat, RosReadCommandId.Ipv6Nat],
                cancellationToken).ConfigureAwait(false),
            RawHash = await CombineAsync(
                session,
                [RosReadCommandId.Ipv4Raw, RosReadCommandId.Ipv6Raw],
                cancellationToken).ConfigureAwait(false),
            MangleHash = await CombineAsync(
                session,
                [RosReadCommandId.Ipv4Mangle, RosReadCommandId.Ipv6Mangle],
                cancellationToken).ConfigureAwait(false),
            RoutingHash = await CombineAsync(
                session,
                [
                    RosReadCommandId.RoutingSettings,
                    RosReadCommandId.RoutingTables,
                    RosReadCommandId.RoutingRules,
                    RosReadCommandId.Ipv4StaticRoutes,
                    RosReadCommandId.Ipv6StaticRoutes,
                ],
                cancellationToken).ConfigureAwait(false),
            VrrpHash = await CombineAsync(session, [RosReadCommandId.VrrpInterfaces], cancellationToken)
                .ConfigureAwait(false),
            InterfaceListHash = await CombineAsync(
                session,
                [RosReadCommandId.InterfaceLists, RosReadCommandId.InterfaceListMembers],
                cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<Hash256> CombineAsync(
        RosSession session,
        IReadOnlyList<RosReadCommandId> commandIds,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (RosReadCommandId commandId in commandIds)
        {
            RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
                session,
                commandId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Hash256 digest = ConfigurationFingerprintBuilder.DigestCommandConfiguration(result);
            hasher.AppendData(Encoding.UTF8.GetBytes(commandId.ToString()));
            hasher.AppendData([(byte)0]);
            hasher.AppendData(digest.Bytes);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }
}
