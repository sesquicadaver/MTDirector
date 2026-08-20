using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Fixed production rollback script (Safe Deployment Spec §24).
/// Literals are only validated anchor comments and old/new chain names — never operator text.
/// </summary>
public static class DeploymentWatchdogScript
{
    public const string Header = "# mfc.deployment.watchdog.v1";

    public const string Policy = "read,write";

    public const string DontRequirePermissions = "no";

    /// <summary>
    /// Compare-before-restore decision (Spec §24.2). Unknown/stale third targets are never changed.
    /// </summary>
    public static DeploymentWatchdogRestoreAction DecideRestore(string? currentTarget, string oldTarget, string newTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTarget);
        if (string.Equals(currentTarget, newTarget, StringComparison.Ordinal))
        {
            return DeploymentWatchdogRestoreAction.RestoreOld;
        }

        if (string.Equals(currentTarget, oldTarget, StringComparison.Ordinal))
        {
            return DeploymentWatchdogRestoreAction.NoOp;
        }

        return DeploymentWatchdogRestoreAction.Abort;
    }

    public static bool ShouldApplySet(
        int matchCount,
        string? chain,
        string? action,
        bool disabled,
        string expectedChain,
        string? currentTarget,
        string oldTarget,
        string newTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedChain);
        if (matchCount != 1 || disabled)
        {
            return false;
        }

        if (!string.Equals(chain, expectedChain, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(action, "jump", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return DecideRestore(currentTarget, oldTarget, newTarget) == DeploymentWatchdogRestoreAction.RestoreOld;
    }

    public static string Render(
        IReadOnlyList<AnchorTarget> oldTargets,
        IReadOnlyList<AnchorTarget> newTargets,
        IReadOnlyList<AnchorKey> rollbackOrder)
    {
        ArgumentNullException.ThrowIfNull(oldTargets);
        ArgumentNullException.ThrowIfNull(newTargets);
        ArgumentNullException.ThrowIfNull(rollbackOrder);
        Dictionary<AnchorKey, string> oldByKey = oldTargets.ToDictionary(static t => t.Key, static t => t.JumpTarget);
        Dictionary<AnchorKey, string> newByKey = newTargets.ToDictionary(static t => t.Key, static t => t.JumpTarget);
        StringBuilder sb = new();
        sb.AppendLine(Header);
        foreach (AnchorKey key in rollbackOrder)
        {
            if (!oldByKey.TryGetValue(key, out string? oldTarget)
                || !newByKey.TryGetValue(key, out string? newTarget))
            {
                throw new DomainInvariantException(
                    $"Rollback order anchor '{key.Marker}' is missing old/new targets.");
            }

            string comment = key.Marker;
            string chain = BuiltinName(key.Chain);
            EnsureSafeLiteral(comment);
            EnsureSafeLiteral(chain);
            EnsureSafeLiteral(oldTarget);
            EnsureSafeLiteral(newTarget);
            string tree = key.Family == IpAddressFamily.IPv4 ? "/ip firewall filter" : "/ipv6 firewall filter";
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{tree};"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $":local mfcN [:len [find where comment=\"{comment}\"]];"));
            sb.AppendLine(":if ($mfcN = 1) do={");
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  :local mfcId [find where comment=\"{comment}\"];"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  :if ([get $mfcId chain]=\"{chain}\" && [get $mfcId action]=\"jump\" && [get $mfcId disabled]=false) do={{"));
            sb.AppendLine("    :local mfcT [get $mfcId jump-target];");
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    :if ($mfcT = \"{newTarget}\") do={{ set $mfcId jump-target=\"{oldTarget}\" }}"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    :if ($mfcT != \"{oldTarget}\" && $mfcT != \"{newTarget}\") do={{ :error \"mfc-watchdog-abort\" }}"));
            sb.AppendLine("  }");
            sb.AppendLine("}");
        }

        return sb.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static Hash256 HashSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static string BuiltinName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported watchdog chain '{chain}'."),
        };

    private static void EnsureSafeLiteral(string value)
    {
        foreach (char c in value)
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or ':' or '_')
            {
                continue;
            }

            throw new DomainInvariantException("Watchdog script literals must not contain user text.");
        }
    }
}

/// <summary>Result of Spec §24.2 compare-before-restore.</summary>
public enum DeploymentWatchdogRestoreAction : byte
{
    RestoreOld = 0,
    NoOp = 1,
    Abort = 2,
}
