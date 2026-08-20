using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Deployment;

/// <summary>
/// Deterministic deployment watchdog resource names (Safe Deployment Spec §23).
/// Token is the first 16 hex chars of SHA-256(deployment_id ‖ device_id).
/// </summary>
public static class DeploymentWatchdogNames
{
    public const int TokenHexLength = 16;

    public static string Token(DeploymentOperationId deploymentId, DeviceId deviceId)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes(deploymentId.Value.ToString("D") + deviceId.Value.ToString("D")));
        string hex = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        return hex[..TokenHexLength];
    }

    public static string RollbackScript(string token) => $"mfc-rb-s-{RequireToken(token)}";

    public static string DeadlineScheduler(string token) => $"mfc-rb-d-{RequireToken(token)}";

    public static string StartupScheduler(string token) => $"mfc-rb-b-{RequireToken(token)}";

    public static bool IsDeploymentWatchdogName(string? name)
        => name is not null
           && (name.StartsWith("mfc-rb-s-", StringComparison.Ordinal)
               || name.StartsWith("mfc-rb-d-", StringComparison.Ordinal)
               || name.StartsWith("mfc-rb-b-", StringComparison.Ordinal));

    private static string RequireToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (token.Length != TokenHexLength
            || token.Any(static c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new DomainInvariantException(
                $"Deployment watchdog token must be {TokenHexLength} lowercase hex characters.");
        }

        return token;
    }
}
