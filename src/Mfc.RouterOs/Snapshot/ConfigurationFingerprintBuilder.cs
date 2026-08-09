using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.RouterOs.Commands;

namespace Mfc.RouterOs.Snapshot;

/// <summary>
/// Builds critical-menu configuration fingerprints from allowlisted read results.
/// Runtime / observation properties never enter the digest (M1-19 AC#1).
/// </summary>
public static class ConfigurationFingerprintBuilder
{
    private static readonly Hash256 EmptyDigest = Hash256.Create(new byte[Hash256.Size]);

    /// <summary>
    /// Digests configuration-classified properties from a command result.
    /// ObservationTyped / TransientExcluded / RawOnly / Forbidden values are ignored.
    /// </summary>
    public static Hash256 DigestCommandConfiguration(RosReadCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RosReadCommandDefinition definition = RosReadCommandRegistry.Get(result.CommandId);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes(result.CommandId.ToString()));

        if (!result.IsSuccess)
        {
            hasher.AppendData("unavailable"u8);
            return Hash256.Create(hasher.GetHashAndReset());
        }

        int ordinal = 0;
        foreach (RosReadRecord record in result.Records)
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            foreach (RosPropertyDefinition property in definition.PropertyProfile.Properties)
            {
                if (!IsConfigurationClassification(property.Classification))
                {
                    continue;
                }

                if (!record.KnownProperties.TryGetValue(property.RouterOsName, out string? value)
                    || value is null)
                {
                    continue;
                }

                hasher.AppendData(Encoding.UTF8.GetBytes(property.RouterOsName));
                hasher.AppendData([(byte)0]);
                hasher.AppendData(Encoding.UTF8.GetBytes(value));
                hasher.AppendData([(byte)0]);
            }

            ordinal++;
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>Combines per-command digests into one menu fingerprint.</summary>
    public static MenuFingerprint BuildMenuFingerprint(
        CriticalConfigurationMenu menu,
        IReadOnlyList<(RosReadCommandId CommandId, RosReadCommandResult? Result)> commandResults)
    {
        ArgumentNullException.ThrowIfNull(commandResults);

        if (menu == CriticalConfigurationMenu.ManagedAnchors)
        {
            // No allowlisted anchor menu yet — stable empty fingerprint (optional section).
            return new MenuFingerprint
            {
                Menu = menu,
                Digest = EmptyDigest,
                Available = false,
            };
        }

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        bool anySuccess = false;
        foreach ((RosReadCommandId commandId, RosReadCommandResult? result) in commandResults)
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(commandId.ToString()));
            if (result is null)
            {
                hasher.AppendData("missing"u8);
                continue;
            }

            Hash256 commandDigest = DigestCommandConfiguration(result);
            hasher.AppendData(commandDigest.Bytes);
            anySuccess |= result.IsSuccess;
        }

        return new MenuFingerprint
        {
            Menu = menu,
            Digest = Hash256.Create(hasher.GetHashAndReset()),
            Available = anySuccess,
        };
    }

    /// <summary>Builds a full ordered fingerprint set for all critical menus.</summary>
    public static ConfigurationFingerprintSet BuildSet(
        IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> resultsByCommand)
    {
        ArgumentNullException.ThrowIfNull(resultsByCommand);
        List<MenuFingerprint> menus = new(CriticalConfigurationMenus.All.Count);
        foreach (CriticalConfigurationMenu menu in CriticalConfigurationMenus.All)
        {
            IReadOnlyList<RosReadCommandId> commands = CriticalConfigurationMenus.CommandsFor(menu);
            List<(RosReadCommandId, RosReadCommandResult?)> pairs = new(commands.Count);
            foreach (RosReadCommandId commandId in commands)
            {
                resultsByCommand.TryGetValue(commandId, out RosReadCommandResult? result);
                pairs.Add((commandId, result));
            }

            menus.Add(BuildMenuFingerprint(menu, pairs));
        }

        return new ConfigurationFingerprintSet(menus);
    }

    /// <summary>True when classification contributes to configuration fingerprints.</summary>
    public static bool IsConfigurationClassification(RosPropertyClassification classification)
        => classification is RosPropertyClassification.ConfigTyped
            or RosPropertyClassification.ConfigOpaque;

    /// <summary>Asserts no observation-only command is used as a fingerprint source.</summary>
    public static bool IsObservationOnlyCommand(RosReadCommandId commandId)
        => commandId is RosReadCommandId.Ipv4DefaultRouteState
            or RosReadCommandId.Ipv6DefaultRouteState;
}
